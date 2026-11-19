using BimGo.Game.Guns;
using BimGo.Platform;
using BimGo.Scene;

// The class belongs to the Game namespace
namespace BimGo.Game
{
    /// <summary>
    /// Text that names keys (input round B2), in one place: built from the current bindings and cached until one
    /// changes (<see cref="KeyHint"/>), so panels drawn every frame show the user's keys without allocating. Toasts
    /// (made once) read <see cref="Hotkeys.Label"/> directly. The guns keep their own panel hints beside their code.
    /// </summary>
    internal sealed partial class GameSession
    {
        #region Helpers

        /// <summary>An action's key as shown in sentences ("Shift+O").</summary>
        private static string Key(InputAction action) => Hotkeys.Label(action);

        /// <summary>An action's key in capitals, for labels and buttons ("SHIFT+O").</summary>
        private static string KeyUpper(InputAction action) => Hotkeys.Label(action).ToUpperInvariant();

        #endregion

        #region HUD, help

        private static readonly KeyHint HINT_HELP = new(() => $"{Key(InputAction.Help)}: Help");
        private static readonly KeyHint HINT_MAP_KEY = new(() => KeyUpper(InputAction.Minimap));
        private static readonly KeyHint HINT_REFRESH_KEY = new(() => KeyUpper(InputAction.Refresh));

        // The gun bar's key labels (gun.Key) follow the bindings
        private int _gunKeysVersion = int.MinValue;

        /// <summary>Gives each gun its selection key's label (the gun bar's corner number).</summary>
        private void RefreshGunKeys()
        {
            if (_guns == null) { return; }
            for (int i = 0; i < _guns.Length; i++)
            {
                KeyChord chord = Hotkeys.Map.Primary(InputCatalog.GunAction(i == 2 ? 3 : i));
                _guns[i].Key = chord.IsEmpty ? string.Empty : Hotkeys.Map.Display(chord);
            }
            _gunKeysVersion = Hotkeys.Version;
        }

        #endregion

        #region Sun panel, section box

        // Polish round P-A2: the docks' footer hints (CLOSE is a button beside them)
        private static readonly KeyHint HINT_SUN_FOOT = new(() => $"{Key(InputAction.Shadows)} / Esc closes");
        private static readonly KeyHint HINT_SUN_KEYS = new(() =>
            $"{Hotkeys.Map.DescribeJoined(" ", false, InputAction.SunEarlier, InputAction.SunLater).ToUpperInvariant()} TIME · {KeyUpper(InputAction.SunPlay)} PLAY");

        private static readonly KeyHint HINT_FIXTURE_FOOT = new(() => $"{Key(InputAction.FixtureProfile)} / Esc closes"); // Round Y-C2: the fixture dock

        private static readonly KeyHint HINT_SECTION_DRAG = new(() => $"Drag a handle along its arrow ({Key(InputAction.SectionFreeDrag)}: no snap) · RMB-drag to look");
        private static readonly KeyHint HINT_SECTION_CLEAR = new(() => $"CLEAR ALL CUTS ({Key(InputAction.SectionClear)})");
        private static readonly KeyHint HINT_SECTION_FOOT = new(() => $"{Key(InputAction.SectionBox)} / Esc · the cut stays");

        #endregion

        #region Markup, bookmarks, tour, library, textures

        private static readonly KeyHint HINT_MARKUP = new(() =>
            $"Drag on the picture to draw · {Key(InputAction.MarkupArrow)} arrow · {Key(InputAction.MarkupBox)} box · {Key(InputAction.MarkupEllipse)} ellipse · " +
            $"{Key(InputAction.MarkupPen)} pen · {Key(InputAction.MarkupText)} text · {Hotkeys.Map.DescribeRange(InputAction.MarkupColour1, 5)} colour · " +
            $"{Key(InputAction.MarkupUndo)} / {Key(InputAction.MarkupRedo)} · {Key(InputAction.MarkupSave)} save · Esc cancel");

        private static readonly KeyHint HINT_BOOKMARKS_KEYS = new(() =>
            $"{KeyUpper(InputAction.AddBookmark)} saves where you stand · {Hotkeys.Map.DescribeRange(InputAction.Bookmark1, 9).ToUpperInvariant()} jump to the first nine");
        private static readonly KeyHint HINT_BOOKMARKS_EMPTY = new(() =>
            $"No bookmarks yet. Resume, stand where you want and press {Key(InputAction.AddBookmark)} (or use ADD THIS VIEW below).");

        private static readonly KeyHint HINT_LIBRARY_PICK = new(() =>
            $"Pick a card: it appears in front of you with Place selected ({Key(InputAction.Gun9)}) · {Key(InputAction.GunSecondary)} commits · Esc discards");

        private static readonly KeyHint HINT_TEXTURES_SAVE = new(() => $"Save ({Key(InputAction.Save)}) embeds the picked images in the file");

        #endregion

    }
}
