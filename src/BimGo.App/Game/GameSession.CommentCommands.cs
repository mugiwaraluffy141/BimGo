using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using BimGo.Audio;
using BimGo.Format;
using BimGo.Platform;
using BimGo.Scene;

namespace BimGo.Game
{
    internal sealed partial class GameSession
    {
        private readonly ContextClickTracker _commentContextClick = new();
        private readonly ContextClickTracker _replyContextClick = new();
        private readonly List<CommentContextRow> _commentContextRows = new();
        private readonly List<ReplyContextRow> _replyContextRows = new();
        private CommentRecord _commentRightTarget, _commentContextDetail;
        private string _replyKeyboardParentId, _replyKeyboardId, _replyRightParentId, _replyRightId;
        private float _commentContextOffset;
        private int _commentContextRevision;
        private bool _commentContextFrameActive;
        private static readonly InputAction[] COMMENT_COMMANDS =
        {
            InputAction.IssuesCopy, InputAction.IssuesDelete, InputAction.IssuesContextMenu
        };

        private readonly record struct CommentContextRow(CommentRecord Record, float X, float Top, float Right, float Bottom);
        private readonly record struct ReplyContextRow(string ParentId, string ReplyId, float X, float Top, float Right, float Bottom);

        private bool CommentCommandsHaveFocus => _commentsOpen && _paused && !_editing && !IsEditingComment
            && _window.IsActive && !OverlayOpen && !IsMarkupOpen && !_menuPreview && !IsCapturingKey
            && !IsControlsPanelOpen && !IsColourProfilesPanelOpen && !IsGeometryPanelOpen
            && !IsBookmarksPanelOpen && !IsEntouragePanelOpen && !IsTexturesPanelOpen
            && !IsLibraryPanelOpen && !IsRoomsPanelOpen && !IsClashesPanelOpen;

        /// <summary>Call after editor/key-capture/markup routing, before global actions. No implicit first-row target.</summary>
        private bool UpdateCommentCommands(InputState input)
        {
            if (!CommentCommandsHaveFocus) { return false; }
            foreach (InputAction action in COMMENT_COMMANDS)
            {
                if (!input.Pressed(action)) { continue; }
                bool list = _commentDetail == null;
                string[] selected = list ? FrozenCommentSelection() : Array.Empty<string>();
                string id = list ? CommentKeyboardTarget(selected) : ResolveCommentCommandTarget(_commentDetail.Id, filtered: false)?.Id;
                (CommentRecord Parent, CommentReply Reply) replyTarget = list ? default : ResolveReplyCommandTarget();
                if (!list && _replyKeyboardId != null && replyTarget.Reply == null)
                {
                    _replyKeyboardParentId = _replyKeyboardId = null;
                    return false; // An invalidated reply must never fall through to its parent issue.
                }
                if (id == null && selected.Length == 0 && replyTarget.Reply == null) { return false; }
                InputAction conflict = InputCatalog.PressedCommandConflict(Hotkeys.Map, action, input, InputCatalog.SIT_ISSUES);
                if (conflict != InputAction.None)
                {
                    Toast($"{InputCatalog.Get(action).Label} suppressed: shortcut conflicts with {InputCatalog.Get(conflict).Label}. Resolve red bindings in CONTROLS.", 4f, important: true);
                    return false; // Leave the user's existing global action available.
                }
                ResetCommentContextClick();
                input.ConsumeFrame();
                switch (action)
                {
                    case InputAction.IssuesCopy:
                        if (replyTarget.Reply != null) { CopyReplyContent(replyTarget.Parent, replyTarget.Reply); }
                        else { CopyCommentContent(ResolveCommentCommandTargets(list && selected.Length > 1 ? selected : new[] { id }, list)); }
                        break;
                    case InputAction.IssuesDelete:
                        if (replyTarget.Reply != null) { RequestReplyDelete(replyTarget.Parent.Id, replyTarget.Reply.Id); }
                        else if (list && selected.Length > 1) { ConfirmFrozenCommentSelection(selected); }
                        else { WithCommentCommandTarget(id, list, DeleteComment); }
                        break;
                    case InputAction.IssuesContextMenu:
                        if (replyTarget.Reply != null)
                        {
                            ShowReplyMenu(replyTarget.Parent.Id, replyTarget.Reply.Id,
                                (int)(_window.Width * 0.4f), (int)(_window.Height * 0.3f), input);
                        }
                        else { ShowFrozenCommentMenu(id, selected, list, (int)(_window.Width * 0.4f), (int)(_window.Height * 0.3f), input); }
                        break;
                }
                return true;
            }
            return false;
        }

        private CommentRecord ResolveCommentCommandTarget(string id, bool filtered)
        {
            if (id == null) { return null; }
            foreach (CommentRecord record in Comments.Comments)
            {
                if (record.Id == id && (!filtered || MatchesCommentFilter(record))) { return record; }
            }
            return null;
        }

        private string[] FrozenCommentSelection() => Comments.Comments
            .Where(r => _commentSelected.Contains(r) && MatchesCommentFilter(r)).Select(r => r.Id).ToArray();

        private string CommentKeyboardTarget(string[] selected)
        {
            if (selected.Length == 1) { return selected[0]; }
            CommentRecord anchor = ResolveCommentCommandTarget(_commentAnchor?.Id, filtered: true);
            return anchor != null && (selected.Length == 0 || _commentSelected.Contains(anchor)) ? anchor.Id : null;
        }

        private IEnumerable<CommentRecord> ResolveCommentCommandTargets(IEnumerable<string> ids, bool filtered)
        {
            foreach (string id in ids)
            {
                CommentRecord record = ResolveCommentCommandTarget(id, filtered);
                if (record != null) { yield return record; }
            }
        }

        private void WithCommentCommandTarget(string id, bool filtered, Action<CommentRecord> command)
        {
            CommentRecord record = ResolveCommentCommandTarget(id, filtered);
            if (record != null) { command(record); }
        }

        private void ConfirmFrozenCommentSelection(string[] ids)
        {
            CommentRecord[] records = ResolveCommentCommandTargets(ids, filtered: true).ToArray();
            if (records.Length == 0) { return; }
            // Reuse the panel's selected-confirm path. Its modal overlay prevents selection changes
            // until confirmation/cancellation; never replace its existing validation or unlinking.
            _commentSelected.Clear();
            foreach (CommentRecord record in records) { _commentSelected.Add(record); }
            ConfirmDeleteSelectedComments();
        }

        private void ResetCommentContextClick()
        {
            _commentContextClick.Reset();
            _commentRightTarget = null;
        }

        private void BeginCommentContextRows(InputState input)
        {
            _commentContextRows.Clear();
            _replyContextRows.Clear();
            _commentContextFrameActive = CommentCommandsHaveFocus;
            float offset = (_commentDetail == null ? _commentScroll : _commentDetailScroll).Offset;
            bool lostContext = !CommentCommandsHaveFocus || _commentContextDetail != _commentDetail;
            bool movedRows = input.Wheel != 0 || offset != _commentContextOffset;
            bool resetTarget = lostContext || movedRows;
            if (resetTarget || input.ClickRevision != _commentContextRevision)
            {
                ResetCommentContextClick();
                _replyContextClick.Reset();
                _replyRightParentId = _replyRightId = null;
            }
            if (lostContext)
            {
                ResetReplyTarget();
            }
            _commentContextDetail = _commentDetail;
            _commentContextOffset = offset;
            _commentContextRevision = input.ClickRevision;
        }

        private void RegisterCommentContextRow(CommentRecord record, float x, float y, float width, float height, float clipTop, float clipBottom)
        {
            float top = MathF.Max(y, clipTop), bottom = MathF.Min(y + height, clipBottom);
            if (bottom > top) { _commentContextRows.Add(new(record, x, top, x + width, bottom)); }
        }

        private void RegisterReplyContextRow(CommentRecord parent, CommentReply reply, float x, float y, float width, float height,
            float clipTop, float clipBottom)
        {
            float top = MathF.Max(y, clipTop), bottom = MathF.Min(y + height, clipBottom);
            if (bottom > top) { _replyContextRows.Add(new(parent.Id, reply.Id, x, top, x + width, bottom)); }
        }

        private CommentRecord CommentContextTarget(int x, int y)
        {
            foreach (CommentContextRow row in _commentContextRows)
            {
                if (x >= row.X && x < row.Right && y >= row.Top && y < row.Bottom) { return row.Record; }
            }
            return null;
        }

        private ReplyContextRow? ReplyContextTarget(int x, int y)
        {
            foreach (ReplyContextRow row in _replyContextRows)
            {
                if (x >= row.X && x < row.Right && y >= row.Top && y < row.Bottom) { return row; }
            }
            return null;
        }

        // The ordered native events determine the target, never the frame-final cursor position.
        // Called only after row enumeration and clipping have ended.
        private bool ShowCommentContextMenu(InputState input)
        {
            float offset = (_commentDetail == null ? _commentScroll : _commentDetailScroll).Offset;
            if (!_commentContextFrameActive || !CommentCommandsHaveFocus || input.Wheel != 0 || offset != _commentContextOffset
                || input.ClickRevision != _commentContextRevision)
            {
                ResetCommentContextClick();
                return false;
            }
            CommentRecord menuTarget = null;
            int menuX = 0, menuY = 0;
            var drag = SystemInformation.DragSize;
            for (int i = 0; i < input.PointerEvents.Count; i++)
            {
                PointerEvent e = input.PointerEvents[i];
                if (e.Kind is PointerEventKind.Cancel or PointerEventKind.Down)
                {
                    ResetCommentContextClick();
                    menuTarget = null;
                }
                else if (e.Kind == PointerEventKind.RightDown)
                {
                    menuTarget = null;
                    _commentRightTarget = CommentContextTarget(e.X, e.Y);
                    _commentContextClick.Begin(_commentRightTarget?.Id, e.X, e.Y);
                }
                else if (e.Kind == PointerEventKind.Move) { _commentContextClick.Move(e.X, e.Y, drag.Width, drag.Height); }
                else if (e.Kind == PointerEventKind.RightUp)
                {
                    CommentRecord target = CommentContextTarget(e.X, e.Y);
                    if (_commentContextClick.End(target?.Id, e.X, e.Y, drag.Width, drag.Height)
                        && ReferenceEquals(target, _commentRightTarget))
                    {
                        menuTarget = target;
                        menuX = e.X;
                        menuY = e.Y;
                    }
                    _commentRightTarget = null;
                }
            }
            if (menuTarget == null || !Comments.Comments.Contains(menuTarget)) { return false; }
            _commentSelected.RemoveWhere(r => !Comments.Comments.Contains(r));
            if (!_commentSelected.Contains(menuTarget))
            {
                _commentSelected.Clear();
                _commentSelected.Add(menuTarget);
                _commentAnchor = menuTarget;
            }
            bool list = _commentDetail == null;
            return ShowFrozenCommentMenu(menuTarget.Id, list ? FrozenCommentSelection() : Array.Empty<string>(), list, menuX, menuY, input);
        }

        private bool ShowReplyContextMenu(InputState input)
        {
            if (!_commentContextFrameActive || !CommentCommandsHaveFocus || _commentDetail == null || input.Wheel != 0
                || _commentDetailScroll.Offset != _commentContextOffset || input.ClickRevision != _commentContextRevision)
            {
                _replyContextClick.Reset();
                _replyRightParentId = _replyRightId = null;
                return false;
            }
            string menuParentId = null, menuReplyId = null;
            int menuX = 0, menuY = 0;
            var drag = SystemInformation.DragSize;
            for (int i = 0; i < input.PointerEvents.Count; i++)
            {
                PointerEvent e = input.PointerEvents[i];
                if (e.Kind is PointerEventKind.Cancel or PointerEventKind.Down)
                {
                    _replyContextClick.Reset();
                    _replyRightParentId = _replyRightId = null;
                    menuParentId = menuReplyId = null;
                }
                else if (e.Kind == PointerEventKind.RightDown)
                {
                    ReplyContextRow? target = ReplyContextTarget(e.X, e.Y);
                    _replyRightParentId = target?.ParentId;
                    _replyRightId = target?.ReplyId;
                    _replyContextClick.Begin(ReplyTargetKey(target), e.X, e.Y);
                    menuParentId = menuReplyId = null;
                }
                else if (e.Kind == PointerEventKind.Move) { _replyContextClick.Move(e.X, e.Y, drag.Width, drag.Height); }
                else if (e.Kind == PointerEventKind.RightUp)
                {
                    ReplyContextRow? target = ReplyContextTarget(e.X, e.Y);
                    bool clicked = _replyContextClick.End(ReplyTargetKey(target), e.X, e.Y, drag.Width, drag.Height);
                    if (clicked && target.HasValue && target.Value.ParentId == _replyRightParentId && target.Value.ReplyId == _replyRightId)
                    {
                        menuParentId = target.Value.ParentId;
                        menuReplyId = target.Value.ReplyId;
                        menuX = e.X;
                        menuY = e.Y;
                    }
                    _replyRightParentId = _replyRightId = null;
                }
            }
            if (menuReplyId == null || ResolveReply(menuParentId, menuReplyId).Reply == null) { return false; }
            SetReplyKeyboardTarget(menuParentId, menuReplyId);
            return ShowReplyMenu(menuParentId, menuReplyId, menuX, menuY, input);
        }

        private void RouteCommentBodyFocus(InputState input, CommentRecord record)
        {
            if (_commentDetail != record || !CommentCommandsHaveFocus) { return; }
            foreach (PointerEvent e in input.PointerEvents)
            {
                if (e.Kind != PointerEventKind.Down) { continue; }
                ReplyContextRow? reply = ReplyContextTarget(e.X, e.Y);
                if (reply.HasValue) { SetReplyKeyboardTarget(reply.Value.ParentId, reply.Value.ReplyId); }
                else if (ReferenceEquals(CommentContextTarget(e.X, e.Y), record))
                {
                    _replyKeyboardParentId = _replyKeyboardId = null;
                }
            }
        }

        private static string ReplyTargetKey(ReplyContextRow? row) => row.HasValue
            ? row.Value.ParentId + ":" + row.Value.ReplyId : null;

        private bool ShowReplyMenu(string parentId, string replyId, int x, int y, InputState input)
        {
            (CommentRecord parent, CommentReply reply) = ResolveReply(parentId, replyId);
            if (parent == null || reply == null) { return false; }
            var items = new List<NativeContextMenu.Item>
            {
                new("Copy reply content", Action: () => CopyReplyContent(ResolveReply(parentId, replyId).Parent, ResolveReply(parentId, replyId).Reply),
                    Shortcut: Hotkeys.Label(InputAction.IssuesCopy)),
                new(ReferenceEquals(_replyDeleteArmed, reply) && _clock <= _replyDeleteArmedUntil
                    ? "Delete reply — confirm" : "Delete reply", Action: () => RequestReplyDelete(parentId, replyId),
                    Shortcut: Hotkeys.Label(InputAction.IssuesDelete))
            };
            input.ConsumeFrame();
            NativeContextMenu.Show(_window.Handle, x, y, items, input);
            return true;
        }

        private (CommentRecord Parent, CommentReply Reply) ResolveReplyCommandTarget() =>
            ResolveReply(_replyKeyboardParentId, _replyKeyboardId);

        private (CommentRecord Parent, CommentReply Reply) ResolveReply(string parentId, string replyId)
        {
            CommentRecord parent = ResolveCommentCommandTarget(parentId, filtered: false);
            CommentReply reply = parent?.Replies?.FirstOrDefault(r => r.Id == replyId);
            return (parent, reply);
        }

        private void SetReplyKeyboardTarget(string parentId, string replyId)
        {
            _replyKeyboardParentId = parentId;
            _replyKeyboardId = replyId;
        }

        private void ResetReplyTarget()
        {
            _replyContextClick.Reset();
            _replyKeyboardParentId = _replyKeyboardId = _replyRightParentId = _replyRightId = null;
        }

        private void RequestReplyDelete(string parentId, string replyId)
        {
            (CommentRecord parent, CommentReply reply) = ResolveReply(parentId, replyId);
            if (parent == null || reply == null) { return; }
            if (ReferenceEquals(_replyDeleteArmed, reply) && _clock <= _replyDeleteArmedUntil)
            {
                Comments.RemoveReply(parent, reply);
                _replyDeleteArmed = null;
                _replyKeyboardParentId = _replyKeyboardId = null;
                _commentsNotice = Comments.LastError ?? "Reply deleted";
                Sound.Play(SoundId.Remove);
            }
            else
            {
                _replyDeleteArmed = reply;
                _replyDeleteArmedUntil = _clock + 3f;
                SetReplyKeyboardTarget(parentId, replyId);
                _commentsNotice = "Delete reply armed · repeat within 3 seconds to confirm";
            }
        }

        private void CopyReplyContent(CommentRecord parent, CommentReply reply)
        {
            (CommentRecord resolvedParent, CommentReply resolvedReply) = ResolveReply(parent?.Id, reply?.Id);
            if (resolvedParent == null || resolvedReply == null) { return; }
            var text = new StringBuilder()
                .AppendLine($"Issue: {resolvedParent.Id}")
                .AppendLine($"Reply: {resolvedReply.Id}")
                .AppendLine($"Author: {resolvedReply.Author}")
                .AppendLine($"Created: {resolvedReply.Created:O}")
                .AppendLine().Append(resolvedReply.Text).ToString();
            try
            {
                Clipboard.SetText(text, TextDataFormat.UnicodeText);
                _commentsNotice = "Reply content copied";
            }
            catch (Exception ex) when (ex is ExternalException or ThreadStateException)
            {
                _commentsNotice = "Clipboard unavailable; reply content was not copied";
            }
        }

        /// <summary>Shared pointer/keyboard menu. Every callback resolves captured IDs, not mutable selection or hover.</summary>
        private bool ShowFrozenCommentMenu(string id, string[] selected, bool list, int x, int y, InputState input)
        {
            CommentRecord record = ResolveCommentCommandTarget(id, list);
            bool multiple = list && selected.Length > 1;
            if (record == null && !multiple) { return false; }
            var items = new List<NativeContextMenu.Item>();
            if (record != null)
            {
                items.Add(new("Open", Action: () => WithCommentCommandTarget(id, list, OpenCommentDetail)));
                items.Add(new("Go to view", Action: () => WithCommentCommandTarget(id, list, GoToComment)));
                items.Add(new("Edit text…", Action: () => WithCommentCommandTarget(id, list, BeginCommentEdit)));
                items.Add(new("Copy content", Action: () => CopyCommentContent(ResolveCommentCommandTargets(new[] { id }, list)),
                    Shortcut: multiple ? null : Hotkeys.Label(InputAction.IssuesCopy)));
                items.Add(new(ReferenceEquals(_commentDeleteArmed, record) && _clock <= _commentDeleteArmedUntil
                    ? "Delete — confirm" : "Delete", Action: () => WithCommentCommandTarget(id, list, DeleteComment),
                    Shortcut: multiple ? null : Hotkeys.Label(InputAction.IssuesDelete)));
            }
            if (multiple)
            {
                // Row commands stay singular. Multi-row commands are always explicit.
                items.Add(new($"Copy selected content ({selected.Length})", Action: () =>
                    CopyCommentContent(ResolveCommentCommandTargets(selected, filtered: true)), Shortcut: Hotkeys.Label(InputAction.IssuesCopy)));
                items.Add(new($"Delete selected ({selected.Length})…", Action: () => ConfirmFrozenCommentSelection(selected),
                    Shortcut: Hotkeys.Label(InputAction.IssuesDelete)));
            }
            input.ConsumeFrame();
            NativeContextMenu.Show(_window.Handle, x, y, items, input);
            return true;
        }

        private void OpenCommentDetail(CommentRecord record)
        {
            _commentDetail = record;
            _commentDetailScroll.Reset();
            _replyDeleteArmed = null;
            _commentsNotice = null;
            ResetCommentContextClick();
        }

        private void CopyCommentContent(IEnumerable<CommentRecord> records)
        {
            var text = new StringBuilder();
            foreach (CommentRecord record in records)
            {
                if (text.Length > 0) { text.AppendLine().AppendLine("---").AppendLine(); }
                text.AppendLine($"Issue: {record.Id}")
                    .AppendLine($"Author: {record.Author}")
                    .AppendLine($"Created: {record.Created:O}")
                    .AppendLine($"Level: {record.Level}")
                    .AppendLine($"Status: {CommentStatus.Label(record.Status)}")
                    .AppendLine($"Priority: {CommentPriority.Label(record.Priority)}")
                    .AppendLine($"Assigned to: {record.AssignedTo ?? "Nobody"}");
                if (record.ClashId != null) { text.AppendLine($"Clash: {record.ClashId}"); }
                text.AppendLine().AppendLine(record.Text);
                foreach (CommentReply reply in record.Replies ?? Enumerable.Empty<CommentReply>())
                {
                    text.AppendLine().AppendLine($"Reply by {reply.Author} · {reply.Created:O}").AppendLine(reply.Text);
                }
            }
            if (text.Length == 0) { return; }
            try
            {
                Clipboard.SetText(text.ToString(), TextDataFormat.UnicodeText);
                _commentsNotice = "Comment content copied";
            }
            catch (Exception ex) when (ex is ExternalException or ThreadStateException)
            {
                _commentsNotice = "Clipboard unavailable; comment content was not copied";
            }
        }
    }
}
