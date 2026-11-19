using BimGo.Audio;
using BimGo.Platform;
using BimGo.Rendering;
using BimGo.Scene;

namespace BimGo.Game.Guns
{
    /// <summary>The shared gizmo controls used by PlaceGun.</summary>
    internal static class GizmoPanel
    {
        public static void HandleKeys(GameSession session, GizmoController gizmo, InputState input)
        {
            if (input.Pressed(InputAction.GizmoSnap)) { session.ToggleGizmoSnap(); }
            if (!gizmo.Active) { return; }
            if (input.Pressed(InputAction.GizmoDrop)) { Drop(session, gizmo); }
            if (input.Pressed(InputAction.HoldRotate))
            {
                gizmo.ToggleMode();
                session.Sound.Play(SoundId.UiClick);
            }
            bool rotating = gizmo.Mode == GizmoMode.Rotate;
            if (input.Pressed(InputAction.HoldStepDown)) { if (rotating) { session.StepSnapAngle(-1); } else { session.StepSnapMove(-1); } }
            if (input.Pressed(InputAction.HoldStepUp)) { if (rotating) { session.StepSnapAngle(+1); } else { session.StepSnapMove(+1); } }
        }

        private static string K(InputAction action) => Hotkeys.Label(action);
        private static readonly KeyHint HINT_MOVE = new(() =>
            $"{Hotkeys.Map.DescribeJoined("", true, InputAction.MoveForward, InputAction.MoveLeft, InputAction.MoveBack, InputAction.MoveRight)} · " +
            $"{Hotkeys.Map.DescribeJoined("/", false, InputAction.HoldUp, InputAction.HoldDown)} up/down · {K(InputAction.HoldRotate)} rotate");
        private static readonly KeyHint HINT_ROTATE = new(() =>
            $"{Hotkeys.Map.DescribeJoined("/", false, InputAction.MoveLeft, InputAction.MoveRight)} rotate · {K(InputAction.HoldRotate)} move");
        private static readonly KeyHint HINT_SNAP = new(() =>
            $"{K(InputAction.HoldFine)} fine · {K(InputAction.GizmoSnap)} snap on/off · {K(InputAction.HoldSnapInvert)} flips snap");
        private static readonly KeyHint HINT_STEP_MOVE = new(() =>
            $"{Hotkeys.Map.DescribeJoined("/", false, InputAction.HoldStepDown, InputAction.HoldStepUp)} move step · {K(InputAction.GizmoDrop)} drop to surface");
        private static readonly KeyHint HINT_STEP_ANGLE = new(() =>
            $"{Hotkeys.Map.DescribeJoined("/", false, InputAction.HoldStepDown, InputAction.HoldStepUp)} angle step · {K(InputAction.GizmoDrop)} drop to surface");
        private static readonly KeyHint HINT_COMMIT = new(() => $"{K(InputAction.GunSecondary)} commit · Esc cancel");
        private static readonly KeyHint HINT_SNAP_OFF_KEY = new(() => $" · {K(InputAction.GizmoSnap)} off");
        private static readonly KeyHint HINT_SNAP_FLIP_KEY = new(() => $" · {K(InputAction.HoldSnapInvert)}");
        private static readonly KeyHint HINT_SNAP_STATE_KEY = new(() => $" ({K(InputAction.GizmoSnap)})");
        private static readonly KeyHint HINT_SNAP_OFF = new(() => $"Snap off ({K(InputAction.GizmoSnap)})");
        private static readonly KeyHint HINT_MOVABLE_GIZMO = new(() => $"Movable: {K(InputAction.GunPrimary)} locks on · {K(InputAction.GizmoDrop)} drops to surface");
        private static readonly KeyHint HINT_MOVABLE_CLONE = new(() => $"Movable: {K(InputAction.GunPrimary)} clones");

        public static bool Drop(GameSession session, GizmoController gizmo)
        {
            bool moved = gizmo.DropToSurface(out string message);
            session.Sound.Play(moved ? SoundId.UiClick : SoundId.Error);
            if (message != null) { session.Toast(moved ? $"{message} ({Hotkeys.Label(InputAction.GunSecondary)} commits)" : message); }
            return moved;
        }

        private static TextBuffer AppendSnap(TextBuffer text, GameSession session)
        {
            if (session.SnapMoveMm >= 1000f) { text.Append(session.SnapMoveMm / 1000f, 2).Append(" m"); }
            else { text.Append(session.SnapMoveMm, 0).Append(" mm"); }
            return text.Append(" · ").Append(session.SnapAngleDeg, 0).Append('°');
        }

        public static void Draw(UiBatch ui, GameSession session, GizmoController gizmo, string title, uint titleColour, int hover,
            float x, float y, float width, float scale)
        {
            float s(float value) => value * scale;
            FontAtlas f = ui.Atlas;
            float used = ui.Text(f.Small, x, y, title, titleColour, s(1.1f));
            if (gizmo.Active) { ui.Text(f.Small, x + used, y, gizmo.Mode == GizmoMode.Rotate ? " · ROTATE" : " · MOVE", titleColour, s(1.1f)); }
            y += s(20);
            if (gizmo.Active)
            {
                ElementRecord record = session.Scene.Elements[gizmo.Target.Element];
                ui.TextWrapped(f.Bold, x, y, width, record.Name, UiTheme.TEXT, maxLines: 1);
                y += s(22);
                TextBuffer delta = session.Text.Clear();
                gizmo.DescribeDelta(delta);
                ui.Text(f.Mono, x, y, delta.Span, UiTheme.TEXT);
                y += s(20);
                bool rotating = gizmo.Mode == GizmoMode.Rotate;
                ui.Text(f.Body, x, y, rotating ? HINT_ROTATE.Text : HINT_MOVE.Text, UiTheme.TEXT_SOFT);
                y += s(18);
                if (gizmo.IsSnapping(session.Input))
                {
                    TextBuffer snap = AppendSnap(session.Text.Clear().Append("SNAP "), session).Append(session.GizmoSnap ? HINT_SNAP_OFF_KEY.Text : HINT_SNAP_FLIP_KEY.Text);
                    ui.Text(f.Body, x, y, snap.Span, UiTheme.ACCENT);
                }
                else { ui.Text(f.Body, x, y, HINT_SNAP.Text, UiTheme.TEXT_MUTED); }
                y += s(18);
                ui.Text(f.Body, x, y, rotating ? HINT_STEP_ANGLE.Text : HINT_STEP_MOVE.Text, UiTheme.TEXT_MUTED);
                y += s(18);
                ui.Text(f.Body, x, y, HINT_COMMIT.Text, UiTheme.TEXT_MUTED);
                return;
            }

            TextBuffer state = session.GizmoSnap
                ? AppendSnap(session.Text.Clear().Append("Snap on · "), session).Append(HINT_SNAP_STATE_KEY.Text)
                : session.Text.Clear().Append(HINT_SNAP_OFF.Text);
            ui.TextRight(f.Small, x + width, y - s(20), state.Span, session.GizmoSnap ? UiTheme.ACCENT : UiTheme.TEXT_FAINT, s(0.5f));
            if (hover < 0)
            {
                ui.Text(f.Body, x, y, "Aim at furniture or fittings.", UiTheme.TEXT_MUTED);
                y += s(19);
                ui.Text(f.Body, x, y, "Point-based loadable families only.", UiTheme.TEXT_MUTED);
                return;
            }

            ElementRecord hovered = session.Scene.Elements[hover];
            ui.TextWrapped(f.Bold, x, y, width, hovered.Name, UiTheme.TEXT, maxLines: 1);
            y += s(22);
            ui.TextWrapped(f.Body, x, y, width, hovered.FamilyType, UiTheme.TEXT_SOFT, maxLines: 1);
            y += s(20);
            if (hovered.Movable) { ui.Text(f.Body, x, y, title == "GIZMO" ? HINT_MOVABLE_GIZMO.Text : HINT_MOVABLE_CLONE.Text, UiTheme.GOOD); }
            else
            {
                TextBuffer reason = session.Text.Clear().Append("Can't move: ").Append(hovered.MoveBlockReason);
                ui.TextWrapped(f.Body, x, y, width, reason.Span, UiTheme.DANGER, maxLines: 1);
            }
            y += s(20);
            if (session.EditsLocalOnly) { ui.Text(f.Body, x, y, "Not connected to Revit: walkthrough only", UiTheme.DANGER); }
        }
    }
}
