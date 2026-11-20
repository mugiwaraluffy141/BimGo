using System.Runtime.InteropServices;

namespace BimGo.Platform
{
    /// <summary>Win32's modal menu loop works without a Windows Forms message loop.</summary>
    internal static class NativeContextMenu
    {
        internal readonly record struct Item(string Label, bool Enabled = true, Action Action = null, string Shortcut = null);

        /// <summary>Returns the chosen zero-based item, or -1 on cancellation/failure.</summary>
        public static int Show(nint owner, int clientX, int clientY, IReadOnlyList<Item> items, InputState input)
        {
            int picked = Track(owner, clientX, clientY, items, input);
            if (picked >= 0 && picked < items.Count && items[picked].Enabled) { items[picked].Action?.Invoke(); }
            return picked;
        }

        private static int Track(nint owner, int clientX, int clientY, IReadOnlyList<Item> items, InputState input)
        {
            nint menu = CreatePopupMenu();
            if (menu == 0) { return -1; }
            try
            {
                for (int i = 0; i < items.Count; i++)
                {
                    string label = items[i].Shortcut == null ? items[i].Label : items[i].Label + "\t" + items[i].Shortcut;
                    if (!AppendMenuW(menu, items[i].Enabled ? 0u : 1u, (nuint)(i + 1), label)) { return -1; }
                }
                var point = new Point { X = clientX, Y = clientY };
                if (!ClientToScreen(owner, ref point)) { return -1; }
                // RETURNCMD | NONOTIFY | RIGHTBUTTON: no WM_COMMAND routing or shared WndProc required.
                uint picked = TrackPopupMenuEx(menu, 0x100 | 0x80 | 0x2, point.X, point.Y, owner, 0);
                return picked == 0 ? -1 : (int)picked - 1;
            }
            finally
            {
                DestroyMenu(menu);
                // The menu loop may have dispatched input to the owner, or left the dismissing mouse
                // message queued. Neither that gesture nor stale held keys may reach the panel/model.
                while (PeekMessageW(out _, owner, 0x0200, 0x020E, 1)) { }
                input.ReleaseAll();
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct Point { public int X, Y; }

        [StructLayout(LayoutKind.Sequential)]
        private struct Message
        {
            public nint Window;
            public uint Id;
            public nuint WParam;
            public nint LParam;
            public uint Time;
            public Point Position;
            public uint Private;
        }

        [DllImport("user32.dll")] private static extern nint CreatePopupMenu();
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)] private static extern bool AppendMenuW(nint menu, uint flags, nuint id, string text);
        [DllImport("user32.dll")] private static extern uint TrackPopupMenuEx(nint menu, uint flags, int x, int y, nint owner, nint parameters);
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)] private static extern bool DestroyMenu(nint menu);
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)] private static extern bool ClientToScreen(nint window, ref Point point);
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)] private static extern bool PeekMessageW(out Message message, nint window, uint first, uint last, uint remove);
    }
}
