namespace BimGo.Platform
{
    /// <summary>A context click keeps its original target and never recovers after becoming a drag.</summary>
    internal sealed class ContextClickTracker
    {
        private string _target;
        private int _x, _y;

        public void Begin(string target, int x, int y)
        {
            _target = target;
            _x = x;
            _y = y;
        }

        public void Move(int x, int y, int dragWidth, int dragHeight)
        {
            if (_target != null && (Math.Abs((long)x - _x) >= Math.Max(1, dragWidth / 2)
                || Math.Abs((long)y - _y) >= Math.Max(1, dragHeight / 2))) { Reset(); }
        }

        public bool End(string target, int x, int y, int dragWidth, int dragHeight)
        {
            Move(x, y, dragWidth, dragHeight);
            bool clicked = _target != null && string.Equals(_target, target, StringComparison.Ordinal);
            Reset();
            return clicked;
        }

        public bool Process(PointerEventBatch batch, Func<PointerEvent, string> targetAt, int dragWidth, int dragHeight,
            bool enabled, out string clickedTarget, out PointerEvent clickedAt)
        {
            clickedTarget = null;
            clickedAt = default;
            if (!enabled)
            {
                Reset();
                return false;
            }

            bool clicked = false;
            for (int i = 0; i < batch.Count; i++)
            {
                PointerEvent e = batch.Events[i];
                if (clicked && e.Kind is PointerEventKind.Cancel or PointerEventKind.Down or PointerEventKind.RightDown)
                {
                    clicked = false;
                    clickedTarget = null;
                    clickedAt = default;
                }
                if (e.Kind == PointerEventKind.Cancel || e.Kind is PointerEventKind.Down or PointerEventKind.Up)
                {
                    Reset();
                }
                else if (e.Kind == PointerEventKind.RightDown)
                {
                    string target = targetAt(e);
                    Begin(target, e.X, e.Y);
                    if (target != null) { batch.MarkHandled(i, true); }
                }
                else if (e.Kind == PointerEventKind.Move)
                {
                    Move(e.X, e.Y, dragWidth, dragHeight);
                }
                else if (e.Kind == PointerEventKind.RightUp)
                {
                    string frozen = _target;
                    if (End(targetAt(e), e.X, e.Y, dragWidth, dragHeight) && frozen != null)
                    {
                        batch.MarkHandled(i, true);
                        clickedTarget = frozen;
                        clickedAt = e;
                        clicked = true;
                    }
                }
            }
            return clicked;
        }

        public void Reset() => _target = null;
    }

    internal static class PointerEventOwnership
    {
        public static bool RightButtonOwnedByRect(bool wasOwned, bool rightDown, bool rightPressed, bool sampledDownInside,
            IReadOnlyList<PointerEvent> events, int left, int top, int width, int height)
        {
            bool owned = wasOwned, sawNativeButtonEvent = false;
            foreach (PointerEvent e in events)
            {
                if (e.Kind == PointerEventKind.RightDown)
                {
                    sawNativeButtonEvent = true;
                    owned = e.X >= left && e.X < left + width && e.Y >= top && e.Y < top + height;
                }
                else if (e.Kind is PointerEventKind.RightUp or PointerEventKind.Cancel)
                {
                    sawNativeButtonEvent = true;
                    owned = false;
                }
            }

            if (!rightDown) { return false; }
            if (sawNativeButtonEvent) { return owned; }
            return rightPressed ? sampledDownInside : owned;
        }
    }
}
