using System.Drawing;
using System.Runtime.InteropServices;
using WinForms = System.Windows.Forms;

namespace BimGo.Platform
{
    /// <summary>Owns one native child search HWND; never opens a modal or delays live filtering.</summary>
    internal sealed class NativeSearchEditor : IDisposable
    {
        private readonly nint _owner;
        private readonly Action _releaseViewerInput;
        private readonly WinForms.ContextMenuStrip _menu;
        private Action<string> _changed;
        private Action<bool> _focusChanged;
        private Action<bool> _completed;
        private Action<bool> _tabbed;
        private string _fieldId;
        private bool _syncing;
        private bool _disposed;
        private bool _visible;
        private float _dpiScale;
        private Font _font;
        private string _lastPublished = string.Empty;
        internal NativeTextEditor.PlainTextBox Editor { get; }
        public bool Visible => _visible && !_disposed;
        public bool HasFocus => !_disposed && (Editor.ContainsFocus || _menu.Visible);

        public NativeSearchEditor(nint owner, int maxLength, Action releaseViewerInput)
        {
            if (owner == 0 || !IsWindow(owner)) { throw new ArgumentException("Search requires a live parent HWND.", nameof(owner)); }
            _owner = owner;
            _releaseViewerInput = releaseViewerInput;
            Editor = new NativeTextEditor.PlainTextBox
            {
                CharacterLimit = maxLength,
                AllowNewlines = false,
                Multiline = false,
                AcceptsTab = false,
                DetectUrls = false,
                ShortcutsEnabled = true,
                BorderStyle = WinForms.BorderStyle.None,
                ScrollBars = WinForms.RichTextBoxScrollBars.None,
                WordWrap = false,
                BackColor = Color.FromArgb(0x12, 0x2B, 0x43),
                ForeColor = Color.FromArgb(0xF7, 0xFB, 0xFF),
                AccessibleName = "Clash search",
                Visible = false
            };
            _menu = NativeTextEditor.CreateContextMenu(Editor);
            Editor.ContextMenuStrip = _menu;
            try
            {
                Marshal.SetLastPInvokeError(0);
                if (SetParent(Editor.Handle, owner) == 0 && Marshal.GetLastWin32Error() != 0)
                {
                    throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
                }
            }
            catch { _menu.Dispose(); Editor.Dispose(); throw; }
            Editor.TextChanged += (_, _) => PublishText();
            Editor.CompositionEnded += PublishText;
            Editor.GotFocus += (_, _) => { _releaseViewerInput?.Invoke(); _focusChanged?.Invoke(true); };
            Editor.LostFocus += (_, _) => { _releaseViewerInput?.Invoke(); _focusChanged?.Invoke(HasFocus); };
            _menu.Opened += (_, _) => _focusChanged?.Invoke(true);
            _menu.Closed += (_, _) => _focusChanged?.Invoke(HasFocus);
            Editor.KeyDown += (_, e) =>
            {
                if (!Editor.Composing && e.KeyCode is WinForms.Keys.Enter or WinForms.Keys.Escape or WinForms.Keys.Tab)
                {
                    e.SuppressKeyPress = true;
                    if (e.KeyCode == WinForms.Keys.Tab)
                    {
                        _tabbed?.Invoke(e.Shift);
                        Blur();
                    }
                    else { Complete(e.KeyCode == WinForms.Keys.Enter); }
                }
                else if (e.Control && e.KeyCode == WinForms.Keys.Y)
                {
                    e.SuppressKeyPress = true;
                    if (Editor.CanRedo) { Editor.Redo(); }
                }
            };
        }

        public void Show(Rectangle bounds, float dpiScale, string text, Action<string> changed, Action<bool> focusChanged,
            string fieldId = "default.search", string accessibleName = "Search", Action<bool> completed = null, Action<bool> tabbed = null)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _changed = changed;
            _focusChanged = focusChanged;
            _completed = completed;
            _tabbed = tabbed;
            if (Editor.AccessibleName != accessibleName) { Editor.AccessibleName = accessibleName; }
            string limited = NativeTextEditor.Limit(text, Editor.CharacterLimit, multiline: false);
            _syncing = true;
            // A frame may be drawn between IME composition messages. Its last committed
            // query must not overwrite the native control's in-progress composition.
            bool fieldChanged = !string.Equals(_fieldId, fieldId, StringComparison.Ordinal);
            if (!Editor.Composing && fieldChanged)
            {
                _fieldId = fieldId;
                _lastPublished = limited;
                Editor.Text = limited;
                Editor.ClearUndo();
                Editor.Select(limited.Length, 0);
            }
            else if (!Editor.Composing)
            {
                _lastPublished = limited;
                if (Editor.Text != limited)
                {
                    int caret = Editor.SelectionStart, selection = Editor.SelectionLength;
                    Editor.Text = limited;
                    Editor.Select(Math.Min(caret, limited.Length), Math.Min(selection, limited.Length - Math.Min(caret, limited.Length)));
                }
            }
            if (Editor.Bounds != bounds) { Editor.Bounds = bounds; }
            if (_dpiScale != dpiScale)
            {
                var font = new Font("Segoe UI", 12f * dpiScale, FontStyle.Regular, GraphicsUnit.Pixel);
                Editor.Font = font;
                _font?.Dispose();
                _font = font;
                _dpiScale = dpiScale;
            }
            _visible = true;
            Editor.Visible = true;
            _syncing = false;
        }

        public void Focus()
        {
            if (!Visible) { return; }
            _releaseViewerInput?.Invoke();
            Editor.Focus();
        }

        public void Blur()
        {
            if (_disposed) { return; }
            bool focused = HasFocus;
            _menu.Close();
            if (Editor.ContainsFocus && IsWindow(_owner)) { SetFocus(_owner); }
            if (focused) { _releaseViewerInput?.Invoke(); }
            _focusChanged?.Invoke(false);
        }

        public void Hide()
        {
            if (_disposed || !_visible) { return; }
            Blur();
            Editor.Visible = false;
            _visible = false;
        }

        /// <summary>Called by the viewer's manual pump before TranslateMessage/DispatchMessage.</summary>
        public bool Preprocess(nint hwnd, uint message, nint wParam, nint lParam)
        {
            if (!Visible) { return false; }
            var native = WinForms.Message.Create(hwnd, (int)message, wParam, lParam);
            // ToolStrip context-menu keyboard/mouse routing normally lives in Application.Run.
            if (WinForms.Application.FilterMessage(ref native)) { return true; }
            if (message < 0x0100 || message > 0x0109) { return false; }
            if (hwnd == Editor.Handle && message == 0x0100 && !Editor.Composing && !_menu.Visible
                && (WinForms.Keys)wParam is WinForms.Keys.Enter or WinForms.Keys.Escape or WinForms.Keys.Tab)
            {
                WinForms.Keys key = (WinForms.Keys)wParam;
                if (key == WinForms.Keys.Tab)
                {
                    _tabbed?.Invoke(WinForms.Control.ModifierKeys.HasFlag(WinForms.Keys.Shift));
                    Blur();
                }
                else { Complete(key == WinForms.Keys.Enter); }
                return true; // Do not translate a closing key into a later WM_CHAR.
            }
            WinForms.Control control = WinForms.Control.FromChildHandle(hwnd);
            return control != null && control.PreProcessControlMessage(ref native) == WinForms.PreProcessControlState.MessageProcessed;
        }

        /// <summary>A raw Win32 parent must reflect the notifications a WinForms parent normally routes.</summary>
        public bool ReflectParentMessage(uint message, nint wParam, nint lParam, out nint result)
        {
            result = 0;
            if (_disposed || !Editor.IsHandleCreated) { return false; }
            nint child = message == 0x004E && lParam != 0 ? Marshal.ReadIntPtr(lParam) : lParam;
            if (child != Editor.Handle || (message != 0x0111 && message != 0x004E && message != 0x0133 && message != 0x0138)) { return false; }
            result = SendMessageW(child, (int)message + 0x2000, wParam, lParam);
            return true;
        }

        private void PublishText()
        {
            if (_syncing || Editor.Composing || _disposed) { return; }
            string text = Editor.Text;
            if (text.Length <= Editor.CharacterLimit && text != _lastPublished)
            {
                _lastPublished = text;
                _changed?.Invoke(text);
            }
        }

        private void Complete(bool accepted)
        {
            _completed?.Invoke(accepted);
            Blur();
        }

        public void Dispose()
        {
            if (_disposed) { return; }
            Hide();
            _disposed = true;
            _changed = null;
            _focusChanged = null;
            _completed = null;
            _tabbed = null;
            _menu.Dispose();
            Editor.Dispose(); // This control alone owns/destroys the child HWND.
            _font?.Dispose();
        }

        [DllImport("user32.dll", SetLastError = true)] private static extern nint SetParent(nint child, nint parent);
        [DllImport("user32.dll")] private static extern nint SetFocus(nint window);
        [DllImport("user32.dll")] private static extern bool IsWindow(nint window);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern nint SendMessageW(nint window, int message, nint wParam, nint lParam);
    }
}
