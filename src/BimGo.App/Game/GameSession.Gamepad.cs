using System.Numerics;
using BimGo.Platform;
using BimGo.Scene;

// The class belongs to the Game namespace
namespace BimGo.Game
{
    /// <summary>
    /// Compatibility hooks for controller call sites that are being removed from other session files.
    /// </summary>
    internal sealed partial class GameSession
    {
        /// <summary>
        /// Polish round P-B: the <see cref="InputCatalog.Situations"/> entry the app is in (its name), for the generated F1
        /// sheet. Follows <see cref="UpdateFrame"/>'s order (what has the keys first): change the two together.
        /// </summary>
        private string CurrentSituation()
        {
            if (_tourActive) { return InputCatalog.SIT_TOUR; }
            if (_video != null) { return InputCatalog.SIT_VIDEO; }
            if (_menuPreview) { return InputCatalog.SIT_SETTINGS; }
            if (_paused) { return InputCatalog.SIT_PAUSE; }
            if (_markupOpen) { return InputCatalog.SIT_MARKUP; }
            if (_sunHoursOpen) { return InputCatalog.SIT_STUDY; }
            if (_photoOpen) { return InputCatalog.SIT_PHOTO; }
            if (_sectionOpen) { return InputCatalog.SIT_SECTION; }
            if (_sunPanelOpen) { return InputCatalog.SIT_SUN_PANEL; }
            if (GridsPanelHasKeys) { return InputCatalog.SIT_GRIDS; }
            if (FixtureDockHasKeys) { return InputCatalog.SIT_FIXTURE; } // Round Y-C2
            if (_pathEditOn) { return InputCatalog.SIT_PATH; } // Round K-B
            if (_clashModeOn) { return InputCatalog.SIT_CLASHES; }
            Guns.Gun gun = _guns[_activeGun];
            if (gun.CapturesInput) { return InputCatalog.SIT_HOLDING; }
            if (_unarmed) { return InputCatalog.SIT_WALKING; }
            return gun switch
            {
                Guns.ScanGun => InputCatalog.SIT_SCAN,
                Guns.MeasureGun => InputCatalog.SIT_MEASURE,
                Guns.CommentGun => InputCatalog.SIT_COMMENT,
                Guns.HammerGun => InputCatalog.SIT_DEMOLISH,
                Guns.GizmoGun or Guns.CloneGun or Guns.PlaceGun => InputCatalog.SIT_GIZMO,
                _ => InputCatalog.SIT_WALKING
            };
        }

        /// <summary>
        /// Keyboard bindings for look actions let users turn the camera with their own keys.
        /// </summary>
        private void KeyboardLook(InputState input)
        {
            var look = new Vector2(input.Axis(InputAction.LookRight) - input.Axis(InputAction.LookLeft),
                input.Axis(InputAction.LookUp) - input.Axis(InputAction.LookDown));
            if (look == Vector2.Zero) { return; }
            if (look.LengthSquared() > 1f) { look = Vector2.Normalize(look); }
            float rate = 3f * _frameTime;
            _player.Turn(-look.X * rate, look.Y * rate);
        }

        /// <summary>
        /// Compatibility hook for editor flows; controller input is disabled.
        /// </summary>
        private void KeyboardEditorLook(InputState input) => KeyboardLook(input);
    }
}
