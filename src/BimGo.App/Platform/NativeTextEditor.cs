using System.Drawing;
using System.Globalization;
using System.Runtime.InteropServices;
using WinForms = System.Windows.Forms;

namespace BimGo.Platform
{
    /// <summary>Owned, synchronous editor for the viewer's short-text commit/cancel contract.</summary>
    internal static class NativeTextEditor
    {
        public static bool Show(nint owner, string title, string initial, int maxLength, bool multiline, out string value)
        {
            using var dialog = new WinForms.Form
            {
                Text = title,
                ClientSize = new Size(540, multiline ? 260 : 155),
                StartPosition = WinForms.FormStartPosition.CenterParent,
                FormBorderStyle = WinForms.FormBorderStyle.FixedDialog,
                MinimizeBox = false,
                MaximizeBox = false,
                ShowInTaskbar = false,
                AutoScaleMode = WinForms.AutoScaleMode.Dpi
            };
            using var editor = new PlainTextBox
            {
                Location = new Point(16, 16),
                Size = new Size(508, multiline ? 164 : 59),
                Multiline = multiline,
                AcceptsTab = false,
                DetectUrls = false,
                ShortcutsEnabled = true,
                ScrollBars = multiline ? WinForms.RichTextBoxScrollBars.Vertical : WinForms.RichTextBoxScrollBars.Horizontal,
                WordWrap = multiline,
                // Validate ourselves: native UTF-16 truncation can split a surrogate pair or CRLF.
                MaxLength = int.MaxValue,
                CharacterLimit = maxLength,
                AllowNewlines = multiline,
                Text = Limit(initial, maxLength, multiline)
            };
            var save = new WinForms.Button { Text = "Save", DialogResult = WinForms.DialogResult.OK, Location = new Point(352, dialog.ClientSize.Height - 40) };
            var cancel = new WinForms.Button { Text = "Cancel", DialogResult = WinForms.DialogResult.Cancel, Location = new Point(444, dialog.ClientSize.Height - 40) };
            var hint = new WinForms.Label
            {
                AutoSize = true,
                Location = new Point(16, dialog.ClientSize.Height - 64),
                Text = multiline ? $"Enter saves · Esc cancels · Shift+Enter adds a line · {maxLength} maximum" : $"Enter saves · Esc cancels · {maxLength} maximum"
            };
            dialog.Controls.AddRange(new WinForms.Control[] { editor, hint, save, cancel });
            dialog.AcceptButton = save;
            dialog.CancelButton = cancel;
            editor.KeyDown += (_, e) =>
            {
                if (editor.Composing) { return; }
                if (e.KeyCode == WinForms.Keys.Enter && !(multiline && e.Shift))
                {
                    e.SuppressKeyPress = true;
                    dialog.DialogResult = WinForms.DialogResult.OK;
                }
                else if (e.KeyCode == WinForms.Keys.Escape)
                {
                    e.SuppressKeyPress = true;
                    dialog.DialogResult = WinForms.DialogResult.Cancel;
                }
                else if (e.Control && e.KeyCode == WinForms.Keys.Y)
                {
                    e.SuppressKeyPress = true;
                    if (editor.CanRedo) { editor.Redo(); }
                }
            };
            using var menu = CreateContextMenu(editor);
            editor.ContextMenuStrip = menu;
            dialog.Shown += (_, _) => { editor.Focus(); editor.SelectAll(); editor.ClearUndo(); };
            bool accepted = dialog.ShowDialog(new Owner(owner)) == WinForms.DialogResult.OK;
            value = accepted ? Limit(editor.Text, maxLength, multiline) : initial;
            return accepted;
        }

        /// <summary>The same native edit commands for modal fields and embedded search.</summary>
        internal static WinForms.ContextMenuStrip CreateContextMenu(PlainTextBox editor)
        {
            var menu = new WinForms.ContextMenuStrip();
            var undo = menu.Items.Add("Undo", null, (_, _) => editor.Undo());
            var redo = menu.Items.Add("Redo", null, (_, _) => editor.Redo());
            menu.Items.Add(new WinForms.ToolStripSeparator());
            var cut = menu.Items.Add("Cut", null, (_, _) => editor.Cut());
            var copy = menu.Items.Add("Copy", null, (_, _) => editor.Copy());
            var paste = menu.Items.Add("Paste", null, (_, _) => editor.PastePlainText());
            var delete = menu.Items.Add("Delete", null, (_, _) => editor.SelectedText = string.Empty);
            menu.Items.Add(new WinForms.ToolStripSeparator());
            var selectAll = menu.Items.Add("Select All", null, (_, _) => editor.SelectAll());
            ((WinForms.ToolStripMenuItem)undo).ShortcutKeyDisplayString = "Ctrl+Z";
            ((WinForms.ToolStripMenuItem)redo).ShortcutKeyDisplayString = "Ctrl+Y";
            ((WinForms.ToolStripMenuItem)cut).ShortcutKeyDisplayString = "Ctrl+X";
            ((WinForms.ToolStripMenuItem)copy).ShortcutKeyDisplayString = "Ctrl+C";
            ((WinForms.ToolStripMenuItem)paste).ShortcutKeyDisplayString = "Ctrl+V";
            ((WinForms.ToolStripMenuItem)delete).ShortcutKeyDisplayString = "Delete";
            ((WinForms.ToolStripMenuItem)selectAll).ShortcutKeyDisplayString = "Ctrl+A";
            menu.Opening += (_, _) =>
            {
                undo.Enabled = editor.CanUndo;
                redo.Enabled = editor.CanRedo;
                cut.Enabled = copy.Enabled = editor.SelectionLength > 0;
                delete.Enabled = editor.SelectionLength > 0;
                selectAll.Enabled = editor.TextLength > 0;
                paste.Enabled = editor.CanPaste(WinForms.DataFormats.GetFormat(WinForms.DataFormats.UnicodeText));
            };
            return menu;
        }

        /// <summary>Keep existing UTF-16 budgets, without cutting a Unicode text element or newline.</summary>
        internal static string Limit(string text, int maxLength, bool multiline)
        {
            text = (text ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n');
            if (!multiline) { text = text.Replace('\n', ' '); }
            if (text.Length <= maxLength) { return text; }
            int end = 0;
            foreach (int start in StringInfo.ParseCombiningCharacters(text))
            {
                if (start > maxLength) { break; }
                end = start;
            }
            return text.Substring(0, end);
        }

        private sealed class Owner(nint handle) : WinForms.IWin32Window
        {
            public nint Handle => handle;
        }

        internal sealed class PlainTextBox : WinForms.RichTextBox
        {
            public bool Composing { get; private set; }
            internal event Action CompositionEnded;
            public int CharacterLimit { get; set; } = int.MaxValue;
            public bool AllowNewlines { get; set; }
            private char _pendingHighSurrogate;
            private int _compositionBudget;

            public PlainTextBox()
            {
                MaxLength = int.MaxValue;
                // RichEdit's automatic font switching can reset undo history partway through
                // mixed-script insertion (notably emoji). Keep native font fallback instead.
                LanguageOption &= ~WinForms.RichTextBoxLanguageOptions.AutoFont;
            }

            // RichTextBox's setter streams text into RichEdit rather than sending WM_CHAR.
            // Validate before either path mutates the native undo/redo stacks.
            public override string SelectedText
            {
                get => base.SelectedText;
                set
                {
                    string normalized = Normalize(value);
                    if (!CanInsert(normalized)) { return; }
                    // EM_REPLACESEL keeps replacement/deletion undoable; the managed streaming
                    // SelectedText setter can lose the replaced selection's undo history.
                    nint pointer = Marshal.StringToHGlobalUni(normalized);
                    try { SendMessageW(Handle, 0x00C2, 1, pointer); }
                    finally { Marshal.FreeHGlobal(pointer); }
                }
            }

            private string Normalize(string value) => (value ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n').Replace("\n", AllowNewlines ? "\n" : " ");

            private bool CanInsert(string value)
            {
                if (value.Length > CharacterLimit - (TextLength - SelectionLength) || !WellFormed(value)) { return false; }
                string current = Text;
                return WellFormed(current.Substring(0, SelectionStart) + value + current.Substring(SelectionStart + SelectionLength));
            }

            private static bool WellFormed(string value)
            {
                for (int i = 0; i < value.Length; i++)
                {
                    if (char.IsHighSurrogate(value[i]))
                    {
                        if (++i >= value.Length || !char.IsLowSurrogate(value[i])) { return false; }
                    }
                    else if (char.IsLowSurrogate(value[i])) { return false; }
                }
                return true;
            }

            internal void InsertPlainText(string value)
            {
                int available = Math.Max(0, CharacterLimit - (TextLength - SelectionLength));
                string limited = Limit(value, available, AllowNewlines);
                if (limited.Length > 0) { SelectedText = limited; }
            }

            protected override bool ProcessCmdKey(ref WinForms.Message message, WinForms.Keys keyData)
            {
                if (keyData == (WinForms.Keys.Control | WinForms.Keys.V) || keyData == (WinForms.Keys.Shift | WinForms.Keys.Insert))
                {
                    PastePlainText();
                    return true;
                }
                return base.ProcessCmdKey(ref message, keyData);
            }

            public void PastePlainText()
            {
                try
                {
                    if (!WinForms.Clipboard.ContainsText(WinForms.TextDataFormat.UnicodeText)) { return; }
                    InsertPlainText(WinForms.Clipboard.GetText(WinForms.TextDataFormat.UnicodeText));
                }
                catch (System.Runtime.InteropServices.ExternalException)
                {
                    // Another process temporarily owns the clipboard; leave the current edit untouched.
                    System.Media.SystemSounds.Beep.Play();
                }
            }

            protected override void WndProc(ref WinForms.Message message)
            {
                const int WM_CHAR = 0x0102, WM_PASTE = 0x0302, EM_REPLACESEL = 0x00C2;
                const int WM_IME_STARTCOMPOSITION = 0x010D, WM_IME_ENDCOMPOSITION = 0x010E, WM_IME_COMPOSITION = 0x010F, GCS_RESULTSTR = 0x0800;
                if (message.Msg == WM_CHAR)
                {
                    char c = (char)message.WParam;
                    if (char.IsHighSurrogate(c)) { _pendingHighSurrogate = c; return; }
                    if (char.IsLowSurrogate(c))
                    {
                        if (_pendingHighSurrogate != '\0') { SelectedText = new string(new[] { _pendingHighSurrogate, c }); }
                        _pendingHighSurrogate = '\0';
                        return;
                    }
                    _pendingHighSurrogate = '\0';
                    // Backspace and Ctrl editing characters must still reach RichEdit.
                    if (c >= ' ' || c == '\r' || c == '\n')
                    {
                        string text = Normalize(c.ToString());
                        if (!CanInsert(text)) { return; }
                    }
                }
                if (message.Msg == EM_REPLACESEL && !CanInsert(Normalize(Marshal.PtrToStringUni(message.LParam)))) { return; }
                if (message.Msg == WM_IME_STARTCOMPOSITION)
                {
                    Composing = true;
                    _compositionBudget = CharacterLimit - (TextLength - SelectionLength);
                }
                if (message.Msg == WM_IME_COMPOSITION && ((long)message.LParam & GCS_RESULTSTR) != 0)
                {
                    nint context = ImmGetContext(Handle);
                    try
                    {
                        int bytes = ImmGetCompositionStringW(context, GCS_RESULTSTR, null, 0);
                        var result = new byte[Math.Max(0, bytes)];
                        if (bytes > 0) { ImmGetCompositionStringW(context, GCS_RESULTSTR, result, bytes); }
                        string text = Normalize(System.Text.Encoding.Unicode.GetString(result));
                        if (text.Length > _compositionBudget || !WellFormed(text))
                        {
                            // Cancel before RichEdit inserts a result; no corrective edit/undo unit.
                            ImmNotifyIME(context, 0x0015, 0x0004, 0); // NI_COMPOSITIONSTR, CPS_CANCEL
                            return;
                        }
                    }
                    finally { if (context != 0) { ImmReleaseContext(Handle, context); } }
                }
                if (message.Msg == WM_IME_ENDCOMPOSITION) { Composing = false; }
                if (message.Msg == WM_PASTE)
                {
                    PastePlainText();
                    return;
                }
                base.WndProc(ref message);
                if (message.Msg == WM_IME_ENDCOMPOSITION) { CompositionEnded?.Invoke(); }
            }

            [DllImport("imm32.dll")] private static extern nint ImmGetContext(nint window);
            [DllImport("imm32.dll")] private static extern bool ImmReleaseContext(nint window, nint context);
            [DllImport("imm32.dll")] private static extern int ImmGetCompositionStringW(nint context, int index, [Out] byte[] buffer, int length);
            [DllImport("imm32.dll")] private static extern bool ImmNotifyIME(nint context, int action, int index, int value);
            [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern nint SendMessageW(nint window, int message, nint wParam, nint lParam);
        }
    }
}
