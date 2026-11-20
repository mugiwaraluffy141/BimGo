using System.Numerics;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using BimGo.Audio;
using BimGo.Clashes;
using BimGo.Platform;
using BimGo.Scene;

namespace BimGo.Game
{
    internal sealed partial class GameSession
    {
        private readonly SetupClipboard _setupClipboard = new();
        private object _setupRightTarget, _setupMenuTarget;
        private Vector2 _setupMenuPosition;
        private static readonly InputAction[] SETUP_ACTIONS =
        {
            InputAction.SetupContextMenu, InputAction.SetupRename, InputAction.SetupDelete, InputAction.SetupCopy, InputAction.SetupPaste
        };

        private bool ClashSetupHasFocus => _clashesOpen && _paused && !_editing && !OverlayOpen
            && _clashImport == null && _clashPickerRule == null && !_menuPreview
            && !IsCapturingKey && !IsControlsPanelOpen && !IsColourProfilesPanelOpen && !IsGeometryPanelOpen
            && !IsMarkupOpen && !IsCommentsPanelOpen && !IsBookmarksPanelOpen && !IsEntouragePanelOpen
            && !IsTexturesPanelOpen && !IsLibraryPanelOpen && !IsRoomsPanelOpen;

        private bool ClashSetupSelectionHasFocus => ClashSetupHasFocus && (_clashTab == 0
            ? _clashDocument?.FindTest(_clashSelectedTest) != null : _clashDocument?.FindSet(_clashSelectedSet) != null);

        /// <summary>Call after editor routing, before global/game actions. True consumes the shortcut.</summary>
        private bool UpdateClashSetupCommands(InputState input)
        {
            if (!ClashSetupSelectionHasFocus) { return false; }
            object target = _clashTab == 0 ? _clashDocument.FindTest(_clashSelectedTest) : _clashDocument.FindSet(_clashSelectedSet);
            foreach (InputAction action in SETUP_ACTIONS)
            {
                if (!input.Pressed(action)) { continue; }
                InputAction conflict = InputCatalog.SetupCommandConflict(Hotkeys.Map, action, input);
                if (conflict != InputAction.None)
                {
                    Toast($"{InputCatalog.Get(action).Label} suppressed: shortcut conflicts with {InputCatalog.Get(conflict).Label}. Resolve red bindings in CONTROLS.", 4f, important: true);
                    return false; // Do not consume the user's existing global binding.
                }
                switch (action)
                {
                    case InputAction.SetupContextMenu:
                        _setupMenuTarget = target;
                        _setupMenuPosition = new Vector2(_window.Width * 0.25f, _window.Height * 0.3f);
                        break;
                    case InputAction.SetupRename: RenameClashSetup(target); break;
                    case InputAction.SetupDelete: DeleteClashSetup(target); break;
                    case InputAction.SetupCopy: CopyClashSetup(target); break;
                    case InputAction.SetupPaste: PasteClashSetup(); break;
                }
                input.ConsumeFrame();
                return true;
            }
            return false;
        }

        private void RenameClashSetup(object target)
        {
            if (target is ClashTest test && _clashDocument.Tests.Contains(test))
            {
                BeginTextEdit("TEST NAME", test.Name, 60, text =>
                {
                    if (text.Length == 0) { return; }
                    test.Name = text;
                    ClashSetupChanged();
                });
            }
            else if (target is SearchSet set && _clashDocument.Sets.Contains(set))
            {
                BeginTextEdit("SET NAME", set.Name, 60, text =>
                {
                    if (text.Length == 0) { return; }
                    set.Name = text;
                    ClashSetupChanged();
                });
            }
        }

        private void DuplicateClashSetup(object target)
        {
            // Preserve the existing button's name and insertion behavior (Paste uses unique names).
            if (target is ClashTest test && _clashDocument.Tests.Contains(test))
            {
                var copy = new ClashTest { Name = test.Name + " (copy)", Kind = test.Kind, SetA = test.SetA,
                    SetB = test.SetB, Tolerance = test.Tolerance, Enabled = test.Enabled };
                _clashDocument.Tests.Insert(_clashDocument.Tests.IndexOf(test) + 1, copy);
                _clashSelectedTest = copy.Id;
            }
            else if (target is SearchSet set && _clashDocument.Sets.Contains(set))
            {
                SearchSet copy = set.Copy(newId: true);
                copy.Name = set.Name + " (copy)";
                _clashDocument.Sets.Insert(_clashDocument.Sets.IndexOf(set) + 1, copy);
                _clashSelectedSet = copy.Id;
            }
            else { return; }
            ClashSetupChanged();
        }

        private void DeleteClashSetup(object target)
        {
            if (target is ClashTest test && _clashDocument.Tests.Contains(test))
            {
                if (_clashDeleteArmed != test)
                {
                    _clashDeleteArmed = test;
                    Toast("Click DELETE again to delete the test and its clashes", 3f);
                    return;
                }
                ClashMerge.RemoveTest(_clashDocument, test.Id);
                if (_clashReviewTest == test.Id) { _clashReviewTest = null; }
                ForgetCurrentClashIfGone();
                _clashSelectedTest = _clashDocument.Tests.FirstOrDefault()?.Id;
                Toast("Test deleted", 2f);
            }
            else if (target is SearchSet set && _clashDocument.Sets.Contains(set))
            {
                int usedBy = _clashDocument.Tests.Count(t => t.SetA == set.Id || t.SetB == set.Id);
                if (usedBy > 0)
                {
                    Sound.Play(SoundId.Error);
                    Toast($"“{set.Name}” is used by {usedBy} test{(usedBy == 1 ? string.Empty : "s")}: change {(usedBy == 1 ? "it" : "them")} first", 3.5f);
                    return;
                }
                if (_clashDeleteArmed != set)
                {
                    _clashDeleteArmed = set;
                    Toast("Click DELETE again to delete the set", 3f);
                    return;
                }
                _clashDocument.Sets.Remove(set);
                _clashSelectedSet = _clashDocument.Sets.FirstOrDefault()?.Id;
                Toast("Set deleted", 2f);
            }
            else { return; }
            _clashDeleteArmed = null;
            ClashSetupChanged();
        }

        private void CopyClashSetup(object target)
        {
            ClashTest test = target as ClashTest;
            SearchSet set = target as SearchSet;
            if (test == null && set == null) { return; }
            try
            {
                var data = new DataObject();
                string text = test != null
                    ? $"Test: {test.Name}\r\nKind: {test.Kind}\r\nSet A: {_clashDocument.FindSet(test.SetA)?.Name}\r\nSet B: {(string.IsNullOrEmpty(test.SetB) ? "Set A against itself" : _clashDocument.FindSet(test.SetB)?.Name)}\r\nTolerance (m): {test.Tolerance.ToString(System.Globalization.CultureInfo.InvariantCulture)}\r\nEnabled: {test.Enabled}"
                    : $"Set: {set.Name}\r\nMatch: {(set.MatchAll ? "All rules" : "Any rule")}\r\n" + string.Join("\r\n", set.AllRules().Select(r => $"{SearchSets.FieldLabel(r)} | {r.Operator} | {string.Join(", ", Enumerable.Range(0, r.ValueCount).Select(r.ValueAt))}"));
                data.SetText(text, TextDataFormat.UnicodeText);
                data.SetData(SetupClipboard.Format, false, _setupClipboard.Copy(_clashDocument, Scene.Provenance?.ModelKey, test, set));
                Clipboard.SetDataObject(data, true);
            }
            catch (Exception ex) when (ex is ExternalException or ThreadStateException)
            {
                Toast("Clipboard unavailable; configuration was not copied", 3f);
            }
        }

        private string ReadSetupClipboard()
        {
            try { return Clipboard.GetData(SetupClipboard.Format) as string; }
            catch (Exception ex) when (ex is ExternalException or ThreadStateException) { return null; }
        }

        private void PasteClashSetup()
        {
            string payload = ReadSetupClipboard(), model = Scene.Provenance?.ModelKey;
            if (_clashTab == 0)
            {
                ClashTest copy = _setupClipboard.PasteTest(payload, _clashDocument, model);
                if (copy == null) { Toast("Copy a Test with valid Sets in this model/session first", 3f); return; }
                _clashDocument.Tests.Add(copy);
                _clashSelectedTest = copy.Id;
            }
            else
            {
                SearchSet copy = _setupClipboard.PasteSet(payload, _clashDocument, model);
                if (copy == null) { Toast("Copy a Set in this model/session first", 3f); return; }
                _clashDocument.Sets.Add(copy);
                _clashSelectedSet = copy.Id;
            }
            _clashDeleteArmed = null;
            _clashScroll.ScrollToEnd();
            ClashSetupChanged();
        }

        private void ShowClashSetupMenu(InputState input)
        {
            object target = _setupMenuTarget;
            _setupMenuTarget = null;
            if (target == null || !ClashSetupHasFocus) { return; }
            bool test = target is ClashTest;
            var items = new List<NativeContextMenu.Item>
            {
                new("Rename…", Action: () => RenameClashSetup(target), Shortcut: Hotkeys.Label(InputAction.SetupRename)),
                new("Copy configuration", Action: () => CopyClashSetup(target), Shortcut: Hotkeys.Label(InputAction.SetupCopy)),
                new("Paste as copy", _setupClipboard.CanPaste(ReadSetupClipboard(), _clashDocument, Scene.Provenance?.ModelKey, test),
                    PasteClashSetup, Hotkeys.Label(InputAction.SetupPaste)),
                new("Duplicate", Action: () => DuplicateClashSetup(target)),
                new(_clashDeleteArmed == target ? "Delete — confirm" : "Delete", Action: () => DeleteClashSetup(target), Shortcut: Hotkeys.Label(InputAction.SetupDelete))
            };
            if (target is ClashTest clashTest)
            {
                items.Add(new("Run", Action: () => RequestClashRun(new[] { clashTest })));
                items.Add(new("Review", Action: () => { _clashPanelGroupPick = null; EnterClashMode(null, clashTest.Id); }));
                items.Add(new("Reset results…", Action: () => RequestClashReset(new[] { clashTest })));
            }
            NativeContextMenu.Show(_window.Handle, (int)_setupMenuPosition.X, (int)_setupMenuPosition.Y, items, input);
        }
    }
}
