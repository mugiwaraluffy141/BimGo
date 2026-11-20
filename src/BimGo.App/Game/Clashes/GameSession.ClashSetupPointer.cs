using BimGo.Clashes;
using BimGo.Platform;

namespace BimGo.Game
{
    internal sealed partial class GameSession
    {
        private readonly ContextClickTracker _setupContextClick = new();
        private readonly List<SetupContextRow> _setupContextRows = new();
        private float _setupContextScroll;
        private bool _setupContextLayoutStable;

        private readonly record struct SetupContextRow(object Target, float X, float Top, float Right, float Bottom);

        private void BeginClashSetupContextRows(InputState input)
        {
            _setupContextRows.Clear();
            _setupContextLayoutStable = _setupContextScroll == _clashScroll.Offset;
            if (!ClashSetupHasFocus || _setupRightTarget == null || input.Wheel != 0
                || _setupContextScroll != _clashScroll.Offset)
            {
                _setupContextClick.Reset();
                _setupRightTarget = null;
            }
            _setupContextScroll = _clashScroll.Offset;
        }

        private void RegisterClashSetupContextRow(object target, float x, float rowY, float width, float height, float clipTop, float clipBottom)
        {
            float top = MathF.Max(rowY, clipTop), bottom = MathF.Min(rowY + height, clipBottom);
            if (bottom > top) { _setupContextRows.Add(new SetupContextRow(target, x, top, x + width, bottom)); }
        }

        private object ClashSetupContextTarget(int x, int y)
        {
            for (int i = 0; i < _setupContextRows.Count; i++)
            {
                SetupContextRow row = _setupContextRows[i];
                if (x >= row.X && x < row.Right && y >= row.Top && y < row.Bottom) { return row.Target; }
            }
            return null;
        }

        private static string ClashSetupContextId(object target) => target switch
        {
            ClashTest test => test.Id,
            SearchSet set => set.Id,
            _ => null
        };

        private void ProcessClashSetupContextRows(InputState input)
        {
            if (!ClashSetupHasFocus || input.Wheel != 0 || !_setupContextLayoutStable || _setupContextScroll != _clashScroll.Offset)
            {
                _setupContextClick.Reset();
                _setupRightTarget = null;
                return;
            }
            var drag = System.Windows.Forms.SystemInformation.DragSize;
            for (int i = 0; i < input.PointerEvents.Count; i++)
            {
                PointerEvent e = input.PointerEvents[i];
                if (e.Kind is PointerEventKind.Cancel or PointerEventKind.Down)
                {
                    _setupContextClick.Reset();
                    _setupRightTarget = _setupMenuTarget = null;
                }
                else if (e.Kind == PointerEventKind.RightDown)
                {
                    _setupMenuTarget = null;
                    _setupRightTarget = ClashSetupContextTarget(e.X, e.Y);
                    _setupContextClick.Begin(ClashSetupContextId(_setupRightTarget), e.X, e.Y);
                }
                else if (e.Kind == PointerEventKind.Move)
                {
                    _setupContextClick.Move(e.X, e.Y, drag.Width, drag.Height);
                }
                else if (e.Kind == PointerEventKind.RightUp)
                {
                    object target = ClashSetupContextTarget(e.X, e.Y);
                    bool clicked = _setupContextClick.End(ClashSetupContextId(target), e.X, e.Y, drag.Width, drag.Height);
                    if (clicked && ReferenceEquals(target, _setupRightTarget))
                    {
                        if (target is ClashTest test)
                        {
                            if (_clashSelectedTest != test.Id) { _clashDeleteArmed = null; }
                            _clashSelectedTest = test.Id;
                        }
                        else if (target is SearchSet set)
                        {
                            if (_clashSelectedSet != set.Id) { _clashDeleteArmed = null; _clashRuleScroll.Reset(); }
                            _clashSelectedSet = set.Id;
                        }
                        _setupMenuTarget = target;
                        _setupMenuPosition = new System.Numerics.Vector2(e.X, e.Y);
                    }
                    _setupRightTarget = null;
                }
            }
            if (_setupMenuTarget != null) { input.ConsumeFrame(); }
        }
    }
}
