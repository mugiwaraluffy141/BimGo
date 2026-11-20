using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using BimGo.Platform;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BimGo.Tests
{
    [TestClass]
    public sealed class NativeTextEditorControlTests
    {
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern nint SendMessageW(nint window, int message, nint wParam, nint lParam);

        private static void Sta(Action test)
        {
            Exception error = null;
            var thread = new Thread(() =>
            {
                try { test(); }
                catch (Exception ex) { error = ex; }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.IsBackground = true;
            thread.Start();
            Assert.IsTrue(thread.Join(TimeSpan.FromSeconds(15)), "Native-control STA test timed out.");
            if (error != null) { ExceptionDispatchInfo.Capture(error).Throw(); }
        }

        private static NativeTextEditor.PlainTextBox Editor(string text, int limit)
        {
            var editor = new NativeTextEditor.PlainTextBox
            {
                CharacterLimit = limit,
                AllowNewlines = true,
                Multiline = true,
                DetectUrls = false,
                MaxLength = int.MaxValue,
                Text = text
            };
            Assert.AreNotEqual(nint.Zero, editor.Handle); // Real RichEdit HWND, never shown.
            editor.ClearUndo();
            editor.Select(editor.TextLength, 0);
            return editor;
        }

        private static void Character(NativeTextEditor.PlainTextBox editor, char value) =>
            SendMessageW(editor.Handle, 0x0102, value, 0); // WM_CHAR

        [TestMethod]
        public void NativeMiddleInsertionAtLimitDoesNotTrapUndoOrRedo() => Sta(() =>
        {
            using var editor = Editor("ab", 3);
            Character(editor, 'c');
            Assert.AreEqual("abc", editor.Text);
            editor.Select(1, 0);
            Character(editor, 'X');
            Assert.AreEqual("abc", editor.Text);
            Assert.AreEqual(1, editor.SelectionStart);
            for (int i = 0; i < 4; i++)
            {
                editor.Undo();
                Assert.AreEqual("ab", editor.Text, $"Undo cycle {i}");
                editor.Redo();
                Assert.AreEqual("abc", editor.Text, $"Redo cycle {i}");
            }
        });

        [TestMethod]
        public void RejectedSelectedTextPreservesMultipleUndoUnitsAndExistingRedo() => Sta(() =>
        {
            using var editor = Editor("a", 3);
            editor.SelectedText = "b";
            editor.SelectedText = "c";
            editor.Select(1, 0);
            editor.SelectedText = "X";
            Assert.AreEqual("abc", editor.Text);
            editor.Undo();
            Assert.AreEqual("ab", editor.Text);
            // A rejected edit while redo is available must not discard that redo history.
            editor.SelectedText = "toolong";
            Assert.AreEqual("ab", editor.Text);
            Assert.IsTrue(editor.CanRedo);
            editor.Redo();
            Assert.AreEqual("abc", editor.Text);
            for (int i = 0; i < 3; i++)
            {
                editor.Undo();
                Assert.AreEqual("ab", editor.Text);
                editor.Undo();
                Assert.AreEqual("a", editor.Text);
                Assert.IsFalse(editor.CanUndo);
                editor.Redo();
                Assert.AreEqual("ab", editor.Text);
                editor.Redo();
                Assert.AreEqual("abc", editor.Text);
                Assert.IsFalse(editor.CanRedo);
            }
        });

        [TestMethod]
        public void PlainPasteOverSelectionKeepsSuffixAndUnicodeBudgetThroughUndoRedo() => Sta(() =>
        {
            using var editor = Editor("abXYcd", 8);
            editor.Select(2, 2);
            // Four available UTF-16 units: emoji + decomposed accented e; no room for Z.
            editor.InsertPlainText("\U0001F600e\u0301Z");
            Assert.AreEqual("ab\U0001F600e\u0301cd", editor.Text);
            Assert.AreEqual(8, editor.TextLength);
            for (int i = 0; i < 3; i++)
            {
                Assert.IsTrue(editor.CanUndo);
                editor.Undo();
                Assert.AreEqual("abXYcd", editor.Text);
                Assert.IsTrue(editor.CanRedo);
                editor.Redo();
                Assert.AreEqual("ab\U0001F600e\u0301cd", editor.Text);
            }
        });

        [TestMethod]
        public void NativeSurrogatePairCannotBeSplitToFillLastUtf16Unit() => Sta(() =>
        {
            using var editor = Editor("a", 3);
            Character(editor, 'b');
            Character(editor, '\uD83D');
            Assert.AreEqual("ab", editor.Text, "High surrogate must not be inserted alone.");
            Character(editor, '\uDE00');
            Assert.AreEqual("ab", editor.Text, "Pair needs two available UTF-16 units.");
            editor.InsertPlainText("\U0001F600");
            Assert.AreEqual("ab", editor.Text);
            editor.Undo();
            Assert.AreEqual("a", editor.Text);
            editor.Redo();
            Assert.AreEqual("ab", editor.Text);
            editor.Select(1, 1);
            Character(editor, '\uD83D');
            Character(editor, '\uDE00');
            Assert.AreEqual("a\U0001F600", editor.Text);
            editor.Select(2, 1); // RichEdit expands this attempted half-pair selection.
            Assert.AreEqual("\U0001F600", editor.SelectedText);
            editor.SelectedText = string.Empty;
            Assert.AreEqual("a", editor.Text, "Deletion must remove the whole pair, not leave a lone surrogate.");
        });

        [TestMethod]
        public void NativeReplaceSelectionRejectsOverflowAndUnpairedSurrogateWithoutUndoUnit() => Sta(() =>
        {
            using var editor = Editor("ab", 3);
            Character(editor, 'c');
            editor.Select(1, 0);
            foreach (string value in new[] { "X", "\uD83D" })
            {
                nint pointer = Marshal.StringToHGlobalUni(value);
                try { SendMessageW(editor.Handle, 0x00C2, 1, pointer); } // EM_REPLACESEL, undo enabled
                finally { Marshal.FreeHGlobal(pointer); }
                Assert.AreEqual("abc", editor.Text);
            }
            editor.Undo();
            Assert.AreEqual("ab", editor.Text);
        });
    }
}
