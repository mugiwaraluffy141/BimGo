using System;
using System.Collections.Generic;

namespace BimGo.Platform
{
    internal enum PointerEventKind { Down, Up, Move, Cancel, RightDown, RightUp }

    /// <summary>Native message coordinates and time, never a render-frame approximation.</summary>
    internal readonly record struct PointerEvent(PointerEventKind Kind, uint Time, int X, int Y, bool Control = false, bool Shift = false, bool Alt = false)
    {
        public bool Modified => Control || Shift || Alt;
    }

    /// <summary>Recognizes two completed, non-dragging clicks on the same stable row ID.</summary>
    internal sealed class DoubleClickTracker
    {
        private string _firstId, _downId;
        private PointerEvent _first, _down;
        private bool _second;

        public void Reset() { _firstId = _downId = null; _second = false; }

        /// <summary>Route a press to its captured target; dispatch a double click before processing any later event.</summary>
        public bool Route(PointerEvent e, string rowId, Action press, Action activate, uint timeLimit,
            int width, int height, int dragWidth, int dragHeight)
        {
            bool doubleClick = Process(e, rowId, timeLimit, width, height, dragWidth, dragHeight);
            if (e.Kind == PointerEventKind.Down && press != null) { press(); return true; }
            if (doubleClick) { activate(); return true; }
            return false;
        }

        public bool Process(PointerEvent e, string rowId, uint timeLimit, int width, int height, int dragWidth, int dragHeight)
        {
            if (e.Kind is PointerEventKind.Cancel or PointerEventKind.RightDown or PointerEventKind.RightUp || e.Modified) { Reset(); return false; }
            if (e.Kind == PointerEventKind.Move)
            {
                if (_downId != null && !Inside(_down, e, dragWidth, dragHeight)) { Reset(); }
                return false;
            }
            if (rowId == null) { Reset(); return false; }
            if (e.Kind == PointerEventKind.Down)
            {
                _second = _downId == null && _firstId == rowId
                    && unchecked(e.Time - _first.Time) <= timeLimit && Inside(_first, e, width, height);
                _firstId = null;
                _downId = rowId;
                _down = e;
                return false;
            }
            if (_downId != rowId || !Inside(_down, e, dragWidth, dragHeight)) { Reset(); return false; }
            bool result = _second;
            if (result) { Reset(); }
            else { _firstId = rowId; _first = _down; _downId = null; }
            return result;
        }

        // Windows metrics describe a rectangle centred on the first press (right/bottom exclusive).
        private static bool Inside(PointerEvent origin, PointerEvent e, int width, int height) =>
            e.X >= origin.X - width / 2 && e.X < origin.X + (width + 1) / 2
            && e.Y >= origin.Y - height / 2 && e.Y < origin.Y + (height + 1) / 2;
    }

    /// <summary>Reusable snapshot: only indices below Count are valid; storage grows only when capacity is exceeded.</summary>
    internal sealed class PointerEventBatch
    {
        public PointerEvent[] Events { get; private set; } = Array.Empty<PointerEvent>();
        public bool[] Handled { get; private set; } = Array.Empty<bool>();
        public int Count { get; private set; }

        public PointerEventBatch() { }

        public PointerEventBatch(IReadOnlyList<PointerEvent> events) => Capture(events);

        /// <summary>Copy the current stream and clear consumption marks; no allocation within existing capacity.</summary>
        public void Capture(IReadOnlyList<PointerEvent> events)
        {
            int count = events.Count;
            if (Events.Length < count)
            {
                int capacity = Math.Max(count, Math.Max(16, Events.Length * 2));
                Events = new PointerEvent[capacity];
                Handled = new bool[capacity];
            }
            Array.Clear(Handled, 0, count);
            for (int i = 0; i < count; i++) { Events[i] = events[i]; }
            Count = count;
        }

        public void MarkHandled(int index, bool handled) => Handled[index] |= handled;

        public PointerEvent? PendingPress => Pending(PointerEventKind.Down);
        public PointerEvent? PendingRightPress => Pending(PointerEventKind.RightDown);

        public PointerEvent? PendingMousePress
        {
            get
            {
                bool leftSeen = false, rightSeen = false;
                for (int i = Count - 1; i >= 0; i--)
                {
                    PointerEventKind kind = Events[i].Kind;
                    if (kind == PointerEventKind.Down && !leftSeen) { leftSeen = true; }
                    else if (kind == PointerEventKind.RightDown && !rightSeen) { rightSeen = true; }
                    else { continue; }
                    if (!Handled[i]) { return Events[i]; }
                }
                return null;
            }
        }

        private PointerEvent? Pending(PointerEventKind kind)
        {
            for (int i = Count - 1; i >= 0; i--)
            {
                if (Events[i].Kind == kind) { return Handled[i] ? null : Events[i]; }
            }
            return null;
        }
    }
}
