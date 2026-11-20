using System.Drawing;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using BimGo.Platform;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WinForms = System.Windows.Forms;

namespace BimGo.Tests
{
    [TestClass]
    public sealed class NativeSearchEditorTests
    {
        [DllImport("user32.dll")] private static extern nint GetParent(nint window);
        [DllImport("user32.dll")] private static extern bool IsWindow(nint window);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern nint SendMessageW(nint window, int message, nint wParam, nint lParam);

        private static void Sta(Action test)
        {
            Exception error = null;
            var thread = new Thread(() =>
            {
                try
                {
                    WinForms.Application.SetUnhandledExceptionMode(WinForms.UnhandledExceptionMode.ThrowException, threadScope: true);
                    test();
                }
                catch (Exception ex) { error = ex; }
            }) { IsBackground = true };
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            Assert.IsTrue(thread.Join(TimeSpan.FromSeconds(15)), "Native search STA test timed out.");
            if (error != null) { ExceptionDispatchInfo.Capture(error).Throw(); }
        }

        private static readonly Rectangle Bounds = new(10, 10, 120, 24);

        private sealed class RawParent : WinForms.NativeWindow, IDisposable
        {
            public NativeSearchEditor Search { get; set; }
            public int KeyboardMessages { get; private set; }

            public RawParent() => CreateHandle(new WinForms.CreateParams
            {
                Caption = "Native search test parent (hidden)",
                Style = 0x00CF0000, // WS_OVERLAPPEDWINDOW, deliberately no WS_VISIBLE.
                Width = 300,
                Height = 200
            });

            protected override void WndProc(ref WinForms.Message message)
            {
                if (Search != null && Search.ReflectParentMessage((uint)message.Msg, message.WParam, message.LParam, out nint result))
                {
                    message.Result = result;
                    return;
                }
                if (message.Msg is 0x0100 or 0x0102) { KeyboardMessages++; }
                base.WndProc(ref message);
            }

            public void Dispose() { Search?.Dispose(); DestroyHandle(); }
        }

        [TestMethod]
        public void RawWin32ParentReflectionDeliversLiveChangesWithoutBubblingTextToParent() => Sta(() =>
        {
            using var parent = new RawParent();
            using var search = new NativeSearchEditor(parent.Handle, 40, () => { });
            parent.Search = search;
            var changes = new List<string>();
            search.Show(Bounds, 1f, "", changes.Add, _ => { });
            SendMessageW(search.Editor.Handle, 0x0102, 'R', 0);
            Assert.AreEqual("R", changes[^1]);
            search.Editor.SelectAll();
            search.Editor.SelectedText = "";
            Assert.AreEqual("", changes[^1]);
            Assert.AreEqual(0, parent.KeyboardMessages);
            Assert.IsTrue(search.Preprocess(search.Editor.Handle, 0x0100, (int)WinForms.Keys.Escape, 0));
            Assert.AreEqual(0, parent.KeyboardMessages);
            CollectionAssert.AreEqual(new[] { "R", "" }, changes);
        });

        [TestMethod]
        public void LayoutBetweenNativeCompositionMessagesDoesNotOverwriteProvisionalText() => Sta(() =>
        {
            using var parent = new RawParent();
            using var search = new NativeSearchEditor(parent.Handle, 40, () => { });
            parent.Search = search;
            var changes = new List<string>();
            search.Show(Bounds, 1f, "a", changes.Add, _ => { });
            search.Editor.Select(1, 0);
            SendMessageW(search.Editor.Handle, 0x010D, 0, 0); // WM_IME_STARTCOMPOSITION
            search.Editor.SelectedText = "b";
            Assert.IsTrue(search.Editor.Composing);
            search.Show(Bounds, 1f, "a", changes.Add, _ => { });
            Assert.AreEqual("ab", search.Editor.Text);
            Assert.AreEqual(0, changes.Count);
            SendMessageW(search.Editor.Handle, 0x010E, 0, 0); // WM_IME_ENDCOMPOSITION
            CollectionAssert.AreEqual(new[] { "ab" }, changes);
            // This tests our composition-message lifecycle, not real input-method acceptance.
        });

        [TestMethod]
        public void NativeChildPublishesEveryEditAndUndoRedoWithoutCommit() => Sta(() =>
        {
            using var parent = new WinForms.Form(); // HWND only; no Show/Application.Run/OpenGL.
            using var search = new NativeSearchEditor(parent.Handle, 40, () => { });
            var changes = new List<string>();
            search.Show(Bounds, 1f, "", changes.Add, _ => { });
            Assert.AreEqual(parent.Handle, GetParent(search.Editor.Handle));
            foreach (char c in "abc")
            {
                Assert.IsFalse(search.Preprocess(search.Editor.Handle, 0x0102, c, 0));
                SendMessageW(search.Editor.Handle, 0x0102, c, 0);
                Assert.AreEqual(search.Editor.Text, changes[^1]);
            }
            CollectionAssert.AreEqual(new[] { "a", "ab", "abc" }, changes);
            search.Editor.Select(1, 1);
            search.Editor.InsertPlainText("\U0001F600\r\n");
            Assert.AreEqual("a\U0001F600 c", changes[^1]);
            search.Editor.Undo();
            Assert.AreEqual("abc", changes[^1]);
            search.Editor.Redo();
            Assert.AreEqual("a\U0001F600 c", changes[^1]);
        });

        [TestMethod]
        public void RepeatedLayoutAndDpiUpdatesPreserveSelectionAndDoNotRepublishQuery() => Sta(() =>
        {
            using var parent = new WinForms.Form();
            using var search = new NativeSearchEditor(parent.Handle, 40, () => { });
            int changed = 0;
            search.Show(Bounds, 1f, "abcdef", _ => changed++, _ => { });
            search.Editor.Select(2, 3);
            nint handle = search.Editor.Handle;
            foreach (float dpi in new[] { 1f, 1f, 1.5f, 2f, 2f })
            {
                var bounds = new Rectangle(30, 40, (int)(120 * dpi), (int)(24 * dpi));
                search.Show(bounds, dpi, "abcdef", _ => changed++, _ => { });
                Assert.AreEqual(handle, search.Editor.Handle);
                Assert.AreEqual(bounds, search.Editor.Bounds);
                Assert.AreEqual(2, search.Editor.SelectionStart);
                Assert.AreEqual(3, search.Editor.SelectionLength);
            }
            Assert.AreEqual(0, changed);
        });

        [TestMethod]
        public void SwitchingLogicalFieldClearsUndoHistoryAndUpdatesAccessibleName() => Sta(() =>
        {
            using var parent = new WinForms.Form();
            using var search = new NativeSearchEditor(parent.Handle, 40, () => { });
            search.Show(Bounds, 1f, "same", _ => { }, _ => { }, "rooms.find", "Room search");
            search.Editor.Select(4, 0);
            SendMessageW(search.Editor.Handle, 0x0102, 'x', 0);
            Assert.IsTrue(search.Editor.CanUndo);
            search.Show(Bounds, 1f, "samex", _ => { }, _ => { }, "rooms.parameters", "Room parameter search");
            Assert.IsFalse(search.Editor.CanUndo);
            Assert.AreEqual("Room parameter search", search.Editor.AccessibleName);
            Assert.AreEqual("samex", search.Editor.Text);
            Assert.AreEqual(search.Editor.TextLength, search.Editor.SelectionStart);
        });

        [TestMethod]
        public void EnterEscapeAndTabBlurWithoutClearingLiveQuery() => Sta(() =>
        {
            using var parent = new WinForms.Form();
            using var search = new NativeSearchEditor(parent.Handle, 40, () => { });
            foreach (WinForms.Keys key in new[] { WinForms.Keys.Enter, WinForms.Keys.Escape, WinForms.Keys.Tab })
            {
                bool? focus = null;
                search.Show(Bounds, 1f, "live query", _ => Assert.Fail("Blur must not change text."), value => focus = value);
                // Same preprocessing + dispatch contract as the viewer pump, on a real child HWND.
                if (!search.Preprocess(search.Editor.Handle, 0x0100, (int)key, 0))
                {
                    SendMessageW(search.Editor.Handle, 0x0100, (int)key, 0);
                }
                Assert.AreEqual(false, focus);
                Assert.AreEqual("live query", search.Editor.Text);
            }
            search.Hide();
            Assert.IsFalse(search.Visible);
            Assert.IsFalse(search.HasFocus);
        });

        [TestMethod]
        public void EnterAndEscapeReportCompletionWithoutChangingQuery()
        {
            Sta(() =>
            {
                using var parent = new WinForms.Form();
                using var search = new NativeSearchEditor(parent.Handle, 40, () => { });
                var completed = new List<bool>();
                search.Show(Bounds, 1f, "room", _ => { }, _ => { }, "rooms.find", "Room search", completed.Add);
                Assert.IsTrue(search.Preprocess(search.Editor.Handle, 0x0100, (int)WinForms.Keys.Enter, 0));
                search.Show(Bounds, 1f, "room", _ => { }, _ => { }, "rooms.find", "Room search", completed.Add);
                Assert.IsTrue(search.Preprocess(search.Editor.Handle, 0x0100, (int)WinForms.Keys.Escape, 0));
                CollectionAssert.AreEqual(new[] { true, false }, completed);
                Assert.AreEqual("room", search.Editor.Text);
            });
        }

        [TestMethod]
        public void HideRetainsQueryButDisposeDestroysOwnedChildAndCannotReopen() => Sta(() =>
        {
            using var parent = new WinForms.Form();
            var search = new NativeSearchEditor(parent.Handle, 40, () => { });
            search.Show(Bounds, 1f, "ab", _ => { }, _ => { });
            nint child = search.Editor.Handle;
            search.Editor.Select(2, 0);
            SendMessageW(child, 0x0102, 'c', 0);
            search.Hide();
            Assert.IsFalse(search.Visible);
            Assert.IsTrue(IsWindow(child));
            search.Show(Bounds, 1f, "abc", _ => { }, _ => { });
            search.Editor.Undo();
            Assert.AreEqual("ab", search.Editor.Text);
            search.Dispose();
            search.Dispose();
            Assert.IsFalse(IsWindow(child));
            Assert.IsTrue(search.Editor.IsDisposed);
            Assert.IsFalse(search.Visible);
            Assert.ThrowsExactly<ObjectDisposedException>(() => search.Show(Bounds, 1f, "", _ => { }, _ => { }));
            Assert.IsTrue(IsWindow(parent.Handle));
        });
    }
}
