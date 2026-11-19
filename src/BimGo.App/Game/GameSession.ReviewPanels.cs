using System.Numerics;
using BimGo.Audio;
using BimGo.Game.Guns;
using BimGo.Physics;
using BimGo.Platform;
using BimGo.Rendering;
using BimGo.Scene;
using BimGo.Clashes;

namespace BimGo.Game
{
    /// <summary>The always-available review controls shown over the model.</summary>
    internal sealed partial class GameSession
    {
        private const int REVIEW_PANEL_COUNT = 3;
        private const float REVIEW_PANEL_WIDTH = 300f;
        private const float REVIEW_HEADER_HEIGHT = 38f;
        private const float REVIEW_ROW_HEIGHT = 30f;
        private const float REVIEW_GAP = 8f;
        private readonly bool[] _reviewPanelCollapsed = new bool[REVIEW_PANEL_COUNT];
        private readonly float[] _reviewPanelScroll = new float[REVIEW_PANEL_COUNT];
        private bool _reviewLookDragging;
        private bool ReviewLookDragging => _reviewLookDragging;
        private bool _reviewRightHeld, _reviewRightMoved, _reviewPendingSecondary;
        private float _reviewRightDistance;
        private Vector3 _reviewOrbitPivot;
        private Vector3 _reviewOrbitInitialOffset;
        private float _reviewOrbitDistance;
        private float _reviewOrbitStartYaw, _reviewOrbitStartPitch;
        private bool _reviewOrbitActive, _reviewPanActive;
        private static readonly string[] REVIEW_TITLES = { "CLASH REVIEW", "INSPECT", "ISSUES" };
        private static readonly uint[] REVIEW_ACCENTS = { UiTheme.ACCENT, UiTheme.SCAN, UiTheme.COMMENT };
        private static readonly string[][] REVIEW_ACTIONS =
        {
            new[] { "TESTS", "RESULTS", "PREVIOUS", "NEXT", "FOCUS", "REPORTS" },
            new[] { "SELECT / SCAN", "DISTANCE", "AREA", "ANGLE", "CLEARANCE", "SECTION BOX", "FIND ROOM" },
            new[] { "ADD COMMENT", "COMMENTS", "MARKUP", "IMPORT BCF", "EXPORT BCF" }
        };

        private bool ReviewPanelsVisible => !_paused && !_uiHidden
            && !IsEditingComment && !_markupOpen && !OverlayOpen;

        private bool ReviewPanelsUseLeftFallback
        {
            get
            {
                if (!_clashModeOn || _clashTool == CLASH_TOOL_BOX) { return false; }
                float margin = S(UiTheme.COLUMN_MARGIN);
                float right = _window.Width - MathF.Min(S(420), _window.Width * 0.42f) - S(16 + UiTheme.COLUMN_GAP);
                return right - (margin + S(250 + UiTheme.COLUMN_GAP)) < S(220);
            }
        }

        private bool OverReviewPanel(Vector2 mouse)
        {
            if (!ReviewPanelsVisible) { return false; }
            for (int i = 0; i < REVIEW_PANEL_COUNT; i++)
            {
                if (GetReviewPanelBounds(i).Contains(mouse)) { return true; }
            }
            return false;
        }

        private readonly struct ReviewPanelBounds
        {
            public readonly float X, Y, W, H;
            public ReviewPanelBounds(float x, float y, float w, float h) => (X, Y, W, H) = (x, y, w, h);
            public bool Contains(Vector2 p) => p.X >= X && p.X < X + W && p.Y >= Y && p.Y < Y + H;
        }

        private ReviewPanelBounds GetReviewPanelBounds(int index)
        {
            float margin = S(UiTheme.COLUMN_MARGIN);
            float x = ReviewPanelsUseLeftFallback ? margin : margin + S(250 + UiTheme.COLUMN_GAP);
            float right = _clashModeOn && _clashTool != CLASH_TOOL_BOX
                ? _window.Width - MathF.Min(S(420), _window.Width * 0.42f) - S(16 + UiTheme.COLUMN_GAP)
                : _window.Width - margin;
            float width = MathF.Max(0f, MathF.Min(S(REVIEW_PANEL_WIDTH), right - x));
            float y = margin;
            for (int i = 0; i < index; i++)
            {
                y += ReviewPanelHeight(i) + S(REVIEW_GAP);
            }
            return new ReviewPanelBounds(x, y, width, ReviewPanelHeight(index));
        }

        private float ReviewPanelHeight(int index)
        {
            if (_reviewPanelCollapsed[index]) { return S(REVIEW_HEADER_HEIGHT); }
            int rows = (REVIEW_ACTIONS[index].Length + 1) / 2;
            float desired = S(REVIEW_HEADER_HEIGHT + REVIEW_ROW_HEIGHT * rows + 12f);
            float margin = S(UiTheme.COLUMN_MARGIN);
            float available = MathF.Max(S(REVIEW_HEADER_HEIGHT), _window.Height - margin * 2f - S(REVIEW_GAP * (REVIEW_PANEL_COUNT - 1)));
            return MathF.Min(desired, available / REVIEW_PANEL_COUNT);
        }

        private ReviewPanelBounds GetReviewActionBounds(ReviewPanelBounds panel, int panelIndex, int action)
        {
            const int columns = 2;
            int rows = (REVIEW_ACTIONS[panelIndex].Length + columns - 1) / columns;
            float gap = S(6), buttonW = MathF.Max(0f, (panel.W - S(20) - gap) / columns), buttonH = S(26);
            float bodyH = MathF.Max(0f, panel.H - S(REVIEW_HEADER_HEIGHT + 10));
            float maxScroll = MathF.Max(0f, rows * (buttonH + gap) - gap - bodyH);
            int row = action / columns, col = action % columns;
            return new ReviewPanelBounds(panel.X + S(10) + col * (buttonW + gap),
                panel.Y + S(REVIEW_HEADER_HEIGHT + 6) + row * (buttonH + gap) - Math.Clamp(_reviewPanelScroll[panelIndex], 0f, maxScroll), buttonW, buttonH);
        }

        private bool HasReviewClashResults => (_clashList?.Count ?? 0) > 0 || (_clashDocument?.Clashes.Count ?? 0) > 0;

        private bool ReviewActionEnabled(int panel, int action)
        {
            if (panel != 0) { return true; }
            return action switch
            {
                0 or 1 => true,
                2 or 3 or 5 => HasReviewClashResults,
                4 => HasReviewClashResults && (_clashCurrent != null || ClashMultiActive),
                _ => false
            };
        }

        /// <summary>Routes clicks over the review column before tool updates can consume them as model picks.</summary>
        private void UpdateReviewPanelInput(InputState input, bool panelsVisible)
        {
            bool overPanel = false;
            for (int i = 0; panelsVisible && i < REVIEW_PANEL_COUNT; i++)
            {
                ReviewPanelBounds bounds = GetReviewPanelBounds(i);
                if (!bounds.Contains(input.MousePosition)) { continue; }
                overPanel = true;
                if (input.Wheel != 0 && !_reviewPanelCollapsed[i] && input.MousePosition.Y >= bounds.Y + S(REVIEW_HEADER_HEIGHT))
                {
                    int rows = (REVIEW_ACTIONS[i].Length + 1) / 2;
                    float bodyH = MathF.Max(0f, bounds.H - S(REVIEW_HEADER_HEIGHT + 10));
                    float contentH = rows * S(32) - S(6);
                    _reviewPanelScroll[i] = Math.Clamp(_reviewPanelScroll[i] - input.Wheel * S(52), 0f, MathF.Max(0f, contentH - bodyH));
                    input.ConsumeWheel();
                }
                if (input.LeftPressed)
                {
                    if (input.MousePosition.Y < bounds.Y + S(REVIEW_HEADER_HEIGHT)
                        && input.MousePosition.X >= bounds.X + bounds.W - S(42))
                    {
                        _reviewPanelCollapsed[i] = !_reviewPanelCollapsed[i];
                        Sound.Play(SoundId.UiClick);
                    }
                    else if (!_reviewPanelCollapsed[i])
                    {
                        for (int action = 0; action < REVIEW_ACTIONS[i].Length; action++)
                        {
                            ReviewPanelBounds button = GetReviewActionBounds(bounds, i, action);
                            if (ReviewActionEnabled(i, action) && button.Contains(input.MousePosition)
                                && input.MousePosition.Y >= bounds.Y + S(REVIEW_HEADER_HEIGHT) && input.MousePosition.Y < bounds.Y + bounds.H)
                            {
                                Sound.Play(SoundId.UiClick);
                                RunReviewPanelAction(i, action);
                                break;
                            }
                        }
                    }
                    input.ConsumeClicks();
                }
                if (input.RightPressed) { input.ConsumeClicks(); }
                if (input.Wheel != 0) { input.ConsumeWheel(); }
                break;
            }

            if (input.RightPressed)
            {
                _reviewLookDragging = !overPanel;
                _reviewRightHeld = _reviewLookDragging;
                _reviewRightMoved = false;
                _reviewRightDistance = 0f;
                _reviewPendingSecondary = false;
            }
            if (_reviewLookDragging && input.RightDown)
            {
                _reviewRightDistance += MathF.Abs(input.MouseDeltaX) + MathF.Abs(input.MouseDeltaY);
                if (_reviewRightDistance > 2f) { _reviewRightMoved = true; }
            }
            if (!input.RightDown)
            {
                _reviewPendingSecondary = _reviewRightHeld && !_reviewRightMoved;
                _reviewRightHeld = false;
                _reviewLookDragging = false;
            }
        }

        private void CancelReviewPointerGesture()
        {
            _reviewRightHeld = _reviewRightMoved = _reviewPendingSecondary = _reviewLookDragging = false;
            _reviewRightDistance = 0f;
            _reviewOrbitActive = _reviewPanActive = false;
            _sectionDrag = _sectionHover = -1;
            _clashMarquee = false;
            _clashSphereDragging = _clashSphereHandleHover = false;
            _clashMenuLook = false;
        }

        private float ReviewEyeOffset => _player.Controller.HoldCrouch && !_player.Flying
            ? _player.Controller.CrouchEye : _player.Controller.StandEye;
        private Vector3 PlayerEye => _player.Feet + Vector3.UnitZ * ReviewEyeOffset;

        private bool ReviewNavigationBlocked(Vector2 mouse)
        {
            if (OverReviewPanel(mouse) || OverlayOpen || _paused || _viewerCommandsOpen) { return true; }
            if (ViewerToolbarVisible)
            {
                var g = ViewerToolbarGeometry(_window.Width, _window.Height);
                if (mouse.X >= g.X && mouse.X < g.X + 5 * (g.W + g.Gap) - g.Gap && mouse.Y >= g.Y && mouse.Y < g.Y + g.H) { return true; }
            }
            return _clashModeOn && OverClashDock(mouse);
        }

        private void UpdateDesktopReviewNavigation(InputState input, float dt, bool pointerNavigation = true)
        {
            if (!_window.IsActive) { _reviewOrbitActive = _reviewPanActive = false; return; }
            if (_guns[_activeGun].CapturesInput) { _reviewOrbitActive = _reviewPanActive = false; return; }
            if (_clashFlying) { _reviewOrbitActive = _reviewPanActive = false; return; }
            bool blocked = ReviewNavigationBlocked(input.MousePosition);
            Vector3 eye = PlayerEye;
            float yaw = _player.Yaw, pitch = _player.Pitch;
            bool changed = false;
            if (pointerNavigation && input.RightPressed && ReviewLookDragging && !blocked && !_clashMenuLook)
            {
                NavigationRay(input.MousePosition, eye, yaw, pitch, out Vector3 origin, out Vector3 direction);
                _reviewOrbitPivot = Pick(origin, direction, PICK_DISTANCE, out RayHit hit) ? hit.Point : eye + direction * 10f;
                _reviewOrbitInitialOffset = eye - _reviewOrbitPivot;
                _reviewOrbitDistance = _reviewOrbitInitialOffset.Length();
                _reviewOrbitStartYaw = yaw;
                _reviewOrbitStartPitch = pitch;
                _reviewOrbitActive = _reviewOrbitDistance > 1e-3f;
            }
            if (pointerNavigation && !input.RightDown) { _reviewOrbitActive = false; }
            bool orbitOwnsInput = pointerNavigation && _reviewOrbitActive && input.RightDown;
            if (pointerNavigation && _reviewOrbitActive && input.RightDown && ReviewLookDragging && _reviewRightMoved && !blocked && !_clashFlying && !_clashMenuLook)
            {
                if (input.MouseDeltaX != 0f || input.MouseDeltaY != 0f)
                {
                    _player.Look(input.MouseDeltaX, input.MouseDeltaY, _sensitivity, _invertY);
                    yaw = _player.Yaw;
                    pitch = _player.Pitch;
                    float azimuth = MathF.Atan2(_reviewOrbitInitialOffset.Y, _reviewOrbitInitialOffset.X) + AngleDelta(yaw, _reviewOrbitStartYaw);
                    float elevation = MathF.Atan2(_reviewOrbitInitialOffset.Z, MathF.Sqrt(_reviewOrbitInitialOffset.X * _reviewOrbitInitialOffset.X + _reviewOrbitInitialOffset.Y * _reviewOrbitInitialOffset.Y)) - (pitch - _reviewOrbitStartPitch);
                    float cp = MathF.Cos(elevation);
                    Vector3 offset = new(cp * MathF.Cos(azimuth), cp * MathF.Sin(azimuth), MathF.Sin(elevation));
                    eye = _reviewOrbitPivot + offset * _reviewOrbitDistance;
                    changed = true;
                }
            }

            if (orbitOwnsInput)
            {
                if (changed) { SetReviewCameraPose(eye, yaw, pitch); }
                return;
            }

            if (pointerNavigation && input.IsPressed(KeyCodes.MIDDLE_MOUSE)) { _reviewPanActive = !blocked; }
            if (pointerNavigation && !input.IsDown(KeyCodes.MIDDLE_MOUSE)) { _reviewPanActive = false; }
            if (pointerNavigation && _reviewPanActive && input.IsDown(KeyCodes.MIDDLE_MOUSE) && !blocked
                && (input.MouseDeltaX != 0f || input.MouseDeltaY != 0f))
            {
                float distance = NavigationTargetDistance(input.MousePosition, eye);
                float scale = 2f * distance * MathF.Tan(Camera.FovY * 0.5f) / MathF.Max(Camera.ViewportHeight, 1f);
                Vector3 forward = ReviewForward(yaw, pitch), right = ReviewRight(yaw);
                Vector3 up = Vector3.Normalize(Vector3.Cross(right, forward));
                eye += -right * (input.MouseDeltaX * scale) + up * (input.MouseDeltaY * scale);
                changed = true;
            }

            if (pointerNavigation && input.Wheel != 0 && !blocked && (!_clashModeOn || !OverClashDock(input.MousePosition)))
            {
                Vector3 pivot = NavigationPivot(input.MousePosition, eye, yaw, pitch, out _);
                float distance = Math.Clamp(Vector3.Distance(eye, pivot), 0.5f, 2000f);
                float nextDistance = Math.Clamp(distance * MathF.Pow(0.85f, input.Wheel), 0.5f, 2000f);
                eye = pivot + Vector3.Normalize(eye - pivot) * nextDistance;
                changed = true;
                input.ConsumeWheel();
            }

            float forwardAxis = input.Axis(InputAction.MoveForward) - input.Axis(InputAction.MoveBack);
            float sideAxis = input.Axis(InputAction.MoveRight) - input.Axis(InputAction.MoveLeft);
            float verticalAxis = _clashModeOn ? input.Axis(InputAction.ClashFlyUp) - input.Axis(InputAction.ClashFlyDown) : 0f;
            Vector3 move = ReviewForward(yaw, pitch) * forwardAxis + ReviewRight(yaw) * sideAxis + Vector3.UnitZ * verticalAxis;
            if (move.LengthSquared() > 1f) { move = Vector3.Normalize(move); }
            if (move.LengthSquared() > 0f)
            {
                float speed = input.Down(InputAction.Run) ? 24f : 8f;
                eye += move * (speed * Math.Clamp(dt, 0f, 0.1f));
                changed = true;
            }
            if (changed) { SetReviewCameraPose(eye, yaw, pitch); }
            if (pointerNavigation) { KeyboardLook(input); }
        }

        private float NavigationTargetDistance(Vector2 mouse, Vector3 eye)
        {
            NavigationPivot(mouse, eye, _player.Yaw, _player.Pitch, out float distance);
            return distance;
        }

        private Vector3 NavigationPivot(Vector2 mouse, Vector3 eye, float yaw, float pitch, out float distance)
        {
            NavigationRay(mouse, eye, yaw, pitch, out Vector3 origin, out Vector3 direction);
            Vector3 pivot = Pick(origin, direction, PICK_DISTANCE, out RayHit hit) ? hit.Point : origin + direction * 10f;
            distance = Math.Clamp(Vector3.Distance(eye, pivot), 0.5f, 2000f);
            return pivot;
        }

        private void NavigationRay(Vector2 mouse, Vector3 eye, float yaw, float pitch, out Vector3 origin, out Vector3 direction)
        {
            float aspect = MathF.Max(Camera.Aspect, 0.1f);
            float tanH = MathF.Tan(_fov * MathF.PI / 360f), tanV = tanH / aspect;
            float nx = 2f * mouse.X / MathF.Max(_window.Width, 1) - 1f;
            float ny = 1f - 2f * mouse.Y / MathF.Max(_window.Height, 1);
            Vector3 forward = ReviewForward(yaw, pitch), right = ReviewRight(yaw);
            Vector3 up = Vector3.Normalize(Vector3.Cross(right, forward));
            origin = eye;
            direction = Vector3.Normalize(forward + right * (nx * tanH) + up * (ny * tanV));
        }

        private static float AngleDelta(float angle, float from) => MathF.Atan2(MathF.Sin(angle - from), MathF.Cos(angle - from));

        private static Vector3 ReviewRight(float yaw) => new(MathF.Sin(yaw), -MathF.Cos(yaw), 0f);

        private static Vector3 ReviewForward(float yaw, float pitch)
        {
            float cp = MathF.Cos(pitch);
            return new Vector3(cp * MathF.Cos(yaw), cp * MathF.Sin(yaw), MathF.Sin(pitch));
        }

        private void SetReviewCameraPose(Vector3 eye, float yaw, float pitch)
        {
            _player.TeleportTo(eye - Vector3.UnitZ * ReviewEyeOffset, yaw, pitch);
        }

        private void FitReviewView()
        {
            Aabb bounds = Scene.Bounds;
            if (!bounds.IsValid) { return; }
            Vector3 target = bounds.Center;
            Vector3 forward = ReviewForward(_player.Yaw, _player.Pitch);
            float distance = ClashFraming.DistanceToSee(bounds, _fov, Camera.Aspect);
            Camera.FarClip = MathF.Max(FpsCamera.FAR, distance + bounds.Size.Length() * 0.5f + 25f);
            SetReviewCameraPose(target - forward * distance, _player.Yaw, _player.Pitch);
        }

        private void BuildReviewPanels()
        {
            FontAtlas f = _ui.Atlas;
            for (int i = 0; i < REVIEW_PANEL_COUNT; i++)
            {
                ReviewPanelBounds b = GetReviewPanelBounds(i);
                _ui.Panel(b.X, b.Y, b.W, b.H, UiTheme.PANEL_STRONG, UiTheme.PANEL_BORDER);
                _ui.PushClip(b.X + S(6), b.Y, MathF.Max(0f, b.W - S(48)), S(REVIEW_HEADER_HEIGHT));
                _ui.Text(f.Bold, b.X + S(12), b.Y + S(11), REVIEW_TITLES[i], REVIEW_ACCENTS[i], S(1f));
                _ui.PopClip();
                float toggleX = b.X + b.W - S(34);
                string marker = _reviewPanelCollapsed[i] ? "+" : "−";
                if (b.W >= S(42))
                {
                    _ui.RoundedRect(toggleX, b.Y + S(5), S(28), S(28), S(UiTheme.ACTION_RADIUS), UiTheme.CONTROL);
                    _ui.TextCentred(f.Bold, toggleX + S(14), b.Y + S(10), marker, UiTheme.TEXT);
                }
                _ui.Rect(b.X + S(1), b.Y + S(REVIEW_HEADER_HEIGHT), b.W - S(2), MathF.Max(1f, UiScale), UiTheme.DIVIDER);
                if (_reviewPanelCollapsed[i]) { continue; }

                _ui.PushClip(b.X + S(2), b.Y + S(REVIEW_HEADER_HEIGHT), b.W - S(4), MathF.Max(0f, b.H - S(REVIEW_HEADER_HEIGHT)));
                for (int a = 0; a < REVIEW_ACTIONS[i].Length; a++)
                {
                    ReviewPanelBounds button = GetReviewActionBounds(b, i, a);
                    ShellStyle style = ReviewActionStyle(i, a);
                    ShellButton(f, _window.Input, button.X, button.Y, button.W, button.H, REVIEW_ACTIONS[i][a], style, ReviewActionEnabled(i, a));
                }
                _ui.PopClip();
            }
        }

        private void RunReviewPanelAction(int panel, int action)
        {
            if (panel == 0)
            {
                switch (action)
                {
                    case 0:
                        if (_clashModeOn) { ExitClashMode(); }
                        OpenClashes();
                        break;
                    case 1: if (!_clashModeOn) { ToggleClashMode(); } break;
                    case 2:
                        if (!_clashModeOn) { EnterClashMode(null); }
                        StepClash(-1);
                        break;
                    case 3:
                        if (!_clashModeOn) { EnterClashMode(null); }
                        StepClash(+1);
                        break;
                    case 4:
                        if (ClashMultiActive) { GoToSelectedClashes(animate: !_clashInstant); }
                        else if (_clashCurrent != null) { GoToClash(_clashCurrent, animate: !_clashInstant); }
                        break;
                    case 5:
                        if (!_clashModeOn) { EnterClashMode(null); }
                        OpenClashReportCard();
                        break;
                }
                return;
            }
            if (panel == 1)
            {
                if (_clashModeOn) { ExitClashMode(); }
                switch (action)
                {
                    case 0: SelectGun(0); break;
                    case 1: SelectMeasureMode(MeasureGun.MeasureMode.Distance); break;
                    case 2: SelectMeasureMode(MeasureGun.MeasureMode.Area); break;
                    case 3: SelectMeasureMode(MeasureGun.MeasureMode.Angle); break;
                    case 4: SelectMeasureMode(MeasureGun.MeasureMode.Clearance); break;
                    case 5: OpenSectionEditor(); break;
                    case 6: OpenRooms(); break;
                }
                return;
            }
            if (_clashModeOn) { ExitClashMode(); }
            switch (action)
            {
                case 0: SelectGun(2); break;
                case 1:
                    if (!_paused) { SetPaused(true); }
                    OpenComments();
                    break;
                case 2:
                    if (_commentDetail != null) { OpenMarkup(_commentDetail, returnToGame: true); }
                    else if (_commentSelected.Count == 1) { OpenMarkup(_commentSelected.First(), returnToGame: true); }
                    else if (_commentSelected.Count > 1) { Toast("Select one issue in COMMENTS first"); }
                    else
                    {
                        if (!_paused) { SetPaused(true); }
                        OpenComments();
                        Toast("Select an issue in COMMENTS, then choose MARKUP");
                    }
                    break;
                case 3: ImportBcf(); break;
                case 4:
                    if (!_paused) { SetPaused(true); }
                    OpenComments();
                    ExportBcf();
                    break;
            }
        }

        private ShellStyle ReviewActionStyle(int panel, int action)
        {
            if (panel == 1 && action is >= 1 and <= 4 && _activeGun == 1 && !_unarmed
                && (int)_measureGun.Mode == action - 1)
            {
                return ShellStyle.Primary;
            }
            return ShellStyle.Normal;
        }

        private void SelectMeasureMode(MeasureGun.MeasureMode mode)
        {
            SelectGun(1);
            _measureGun.SetMode(mode);
        }
    }
}
