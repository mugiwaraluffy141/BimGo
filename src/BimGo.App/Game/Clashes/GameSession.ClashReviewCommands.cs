using System.Globalization;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using BimGo.Clashes;
using BimGo.Platform;
using BimGo.Scene;

namespace BimGo.Game
{
    internal sealed partial class GameSession
    {
        private readonly ClashReviewContext _clashReviewContext = new();
        private static readonly InputAction[] CLASH_REVIEW_COMMANDS =
        {
            InputAction.ClashCopyInfo, InputAction.ClashGroupRename, InputAction.ClashRowContextMenu
        };

        private bool ClashReviewCommandsHaveFocus => _window.IsActive && _clashModeOn && !_paused
            && !_editing && !IsCapturingKey && !OverlayOpen && !_clashSearchFocused
            && _clashTool == CLASH_TOOL_NONE && _clashBatch == null && _clashReportJob == null
            && !_clashReportCardOpen && !_markupOpen && !_gridsPanelOpen && !FixtureDockHasKeys;

        private ClashRecord ResolveReviewClash(string id)
        {
            EnsureClashGroupIndex();
            return id != null && _clashById.TryGetValue(id, out ClashRecord clash)
                && clash.TestId == _clashReviewTest && _clashList.Contains(clash) ? clash : null;
        }

        private ClashGroup ResolveReviewGroup(string id)
        {
            if (id == null || _clashDocument.Groups == null) { return null; }
            foreach (ClashGroup group in _clashDocument.Groups)
            {
                if (group.Id != id || group.TestId != _clashReviewTest) { continue; }
                // Hidden/removed rows do not retain keyboard focus after a filter or document change.
                foreach (ClashDisplayRow row in _clashDisplay)
                {
                    if (row.Row == null && row.Group?.Id == id) { return group; }
                }
                return null;
            }
            return null;
        }

        private ClashReviewContext.SelectionState ClashReviewSelectionState() => new(_clashDocument, _clashReviewTest,
            _clashCurrent?.Id, _clashGroupCurrent?.Id, _clashAnchor?.Id, _clashSelected.Count);

        private void FocusClashReviewRow(string groupId, string clashId) =>
            _clashReviewContext.Focus(groupId, clashId, ClashReviewSelectionState());

        private void MergeFrozenReviewGroups(string[] ids)
        {
            var groups = new List<ClashGroup>();
            foreach (string id in ids)
            {
                ClashGroup group = _clashDocument.Groups?.FirstOrDefault(g => g.Id == id && g.TestId == _clashReviewTest);
                if (group == null) { return; } // Never silently merge a different/incomplete set of source groups.
                groups.Add(group);
            }
            if (groups.Count >= 2) { MergeClashGroups(groups); }
        }

        private void AddFrozenReviewClashes(string groupId, string[] ids)
        {
            ClashGroup group = ResolveReviewGroup(groupId);
            if (group == null) { return; }
            var clashes = new List<ClashRecord>();
            foreach (string id in ids)
            {
                ClashRecord clash = ResolveReviewClash(id);
                if (clash == null) { return; }
                clashes.Add(clash);
            }
            AddClashesToGroup(group, clashes);
        }

        /// <summary>Row commands never infer their target from the cursor or from a later current-clash value.</summary>
        private bool UpdateClashReviewCommands(InputState input)
        {
            if (!ClashReviewCommandsHaveFocus) { return false; }
            if (!_clashReviewContext.TryGet(ClashReviewSelectionState(), out string groupId, out string clashId))
            {
                groupId = _clashGroupCurrent?.Id;
                clashId = groupId == null ? _clashCurrent?.Id : null;
            }
            ClashGroup group = ResolveReviewGroup(groupId);
            ClashRecord clash = ResolveReviewClash(clashId);
            if (group == null && clash == null) { _clashReviewContext.Reset(); }
            if (group == null && clash == null) { return false; }
            foreach (InputAction action in CLASH_REVIEW_COMMANDS)
            {
                if (!input.Pressed(action) || (action == InputAction.ClashGroupRename && group == null)) { continue; }
                InputAction conflict = InputCatalog.PressedCommandConflict(Hotkeys.Map, action, input, InputCatalog.SIT_CLASHES);
                if (conflict != InputAction.None)
                {
                    Toast($"{InputCatalog.Get(action).Label} suppressed: shortcut conflicts with {InputCatalog.Get(conflict).Label}. Resolve red bindings in CONTROLS.", 4f, important: true);
                    return false;
                }
                input.ConsumeFrame();
                switch (action)
                {
                    case InputAction.ClashCopyInfo: CopyClashReviewInfo(groupId, clashId); break;
                    case InputAction.ClashGroupRename: RenameReviewGroup(groupId); break;
                    case InputAction.ClashRowContextMenu:
                        var row = new ClashDisplayRow { Group = group, Row = clash == null ? null : new ClashRow { Clash = clash } };
                        OpenClashRowMenu(row, new Vector2(_clashDockX + S(20), _clashDockY + S(60)));
                        break;
                }
                return true;
            }
            return false;
        }

        private void RenameReviewGroup(string id)
        {
            ClashGroup group = ResolveReviewGroup(id);
            if (group != null) { RenameClashGroup(group); }
        }

        /// <summary>Only row-menu commands narrow selection. R and the REVIT button retain their bulk contract.</summary>
        private void FocusReviewClash(string id, bool showInRevit)
        {
            ClashRecord clash = ResolveReviewClash(id);
            if (clash == null) { return; }
            ClickClashRow(clash, ctrl: false, shift: false);
            FocusClashReviewRow(null, id);
            if (showInRevit) { ShowClashInRevit(); }
        }

        private void CopyClashReviewInfo(string groupId, string clashId)
        {
            ClashGroup group = ResolveReviewGroup(groupId);
            ClashRecord clash = ResolveReviewClash(clashId);
            if (group == null && clash == null) { return; }
            var text = new StringBuilder();
            text.Append("Model: ").AppendLine(Scene.ModelTitle);
            text.Append("Test: ").AppendLine(_clashDocument.FindTest(group?.TestId ?? clash.TestId)?.Name);
            if (group != null)
            {
                EnsureClashGroupIndex();
                ClashGroupSummary summary = ClashGroups.Summarise(group, _clashById);
                text.Append("Group: ").AppendLine(ClashGroups.DisplayName(group));
                text.Append("Group ID: ").AppendLine(group.Id);
                text.Append("Status: ").AppendLine(group.Status);
                text.Append("Members: ").AppendLine(group.Count.ToString(CultureInfo.InvariantCulture));
                text.Append("Member statuses: ").AppendLine(ClashStatusCounts(summary.ByStatus));
                text.Append("Assigned to: ").AppendLine(group.AssignedTo);
                text.Append("Notes: ").AppendLine(group.Notes);
                text.Append("Clash IDs: ").AppendLine(string.Join(", ", group.ClashIds));
            }
            else
            {
                text.Append("Clash ID: ").AppendLine(clash.Id);
                text.Append("Group: ").AppendLine(ClashGroupOf(clash) is ClashGroup own ? ClashGroups.DisplayName(own) : string.Empty);
                text.Append("Status: ").AppendLine(clash.Status);
                text.Append("Assigned to: ").AppendLine(clash.AssignedTo);
                text.Append("Notes: ").AppendLine(clash.Notes);
                AppendReviewElement(text, "A", clash.A);
                AppendReviewElement(text, "B", clash.B);
                text.Append("Clash point (Revit internal metres): ").AppendLine(FormattableString.Invariant($"{clash.X}, {clash.Y}, {clash.Z}"));
                text.Append(clash.Crossing ? "Depth (m): " : "Clearance (m): ").AppendLine(clash.Distance.ToString(CultureInfo.InvariantCulture));
            }
            try { System.Windows.Forms.Clipboard.SetText(text.ToString(), System.Windows.Forms.TextDataFormat.UnicodeText); }
            catch (Exception ex) when (ex is ExternalException or ThreadStateException)
            {
                Toast("Clipboard unavailable; review information was not copied", 3f, important: true);
            }
        }

        private static void AppendReviewElement(StringBuilder text, string side, ClashElementRef element)
        {
            if (element == null) { return; }
            text.Append("Element ").Append(side).Append(": ").AppendLine(element.Label);
            text.Append("  Element ID: ").AppendLine(element.ElementId.ToString(CultureInfo.InvariantCulture));
            text.Append("  Unique ID: ").AppendLine(element.UniqueId);
            text.Append("  Link: ").AppendLine(element.Link);
            text.Append("  Level: ").AppendLine(element.Level);
        }
    }
}
