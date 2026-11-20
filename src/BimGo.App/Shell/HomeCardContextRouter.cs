using BimGo.Platform;

namespace BimGo.Shell
{
    /// <summary>Hit-tests ordered native messages, never the cursor sampled at the end of a frame.</summary>
    internal sealed class HomeCardContextRouter
    {
        internal readonly record struct Row(string Id, float X, float Y, float Width, float Height);
        internal readonly record struct Request(string Id, int X, int Y);

        private readonly ContextClickTracker _click = new();
        private Row[] _rows = Array.Empty<Row>();

        public void Reset() => _click.Reset();

        public Request? Process(IReadOnlyList<Row> rows, IReadOnlyList<PointerEvent> events, int dragWidth, int dragHeight)
        {
            // Refresh, resize, DPI and changes to card order/visibility must not retarget a held button.
            if (!_rows.SequenceEqual(rows)) { Reset(); _rows = rows.ToArray(); }
            foreach (PointerEvent e in events)
            {
                string target = null;
                foreach (Row row in _rows)
                {
                    if (e.X >= row.X && e.X < row.X + row.Width && e.Y >= row.Y && e.Y < row.Y + row.Height)
                    {
                        target = row.Id;
                        break;
                    }
                }
                switch (e.Kind)
                {
                    case PointerEventKind.Cancel:
                    case PointerEventKind.Down: Reset(); break;
                    case PointerEventKind.RightDown: _click.Begin(target, e.X, e.Y); break;
                    case PointerEventKind.Move: _click.Move(e.X, e.Y, dragWidth, dragHeight); break;
                    case PointerEventKind.RightUp:
                        if (_click.End(target, e.X, e.Y, dragWidth, dragHeight)) { return new Request(target, e.X, e.Y); }
                        break;
                }
            }
            return null;
        }
    }
}
