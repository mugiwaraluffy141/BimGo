using BimGo.Live;
using BimGo.Platform;
using Gl = BimGo.Native.Gl;

namespace BimGo.Shell
{
    internal sealed partial class HomeScreen
    {
        private sealed record HomeContextTarget(string Id, string Path = null, string SessionId = null, string Information = null);
        private readonly HomeCardContextRouter _homeContextRouter = new();
        private readonly List<(HomeCardContextRouter.Row Row, HomeContextTarget Target)> _homeContextRows = new();
        private Func<OpenTarget> _homeCardAction;
        private int _homeContextClickRevision;

        private void RegisterHomeContextCard(HomeContextTarget target, float x, float y, float width, float height) =>
            _homeContextRows.Add((new HomeCardContextRouter.Row(target.Id, x, y, width, height), target));

        private bool ProcessHomeContextMenu(InputState input, out OpenTarget chosen)
        {
            chosen = null;
            if (!_window.IsActive || input.Wheel != 0 || _homeContextClickRevision != input.ClickRevision)
            {
                _homeContextRouter.Reset();
            }
            _homeContextClickRevision = input.ClickRevision;
            if (!_window.IsActive || input.Wheel != 0) { return false; }
            var drag = System.Windows.Forms.SystemInformation.DragSize;
            HomeCardContextRouter.Request? request = _homeContextRouter.Process(
                _homeContextRows.Select(r => r.Row).ToArray(), input.PointerEvents, drag.Width, drag.Height);
            if (!request.HasValue) { return false; }
            HomeContextTarget target = _homeContextRows.First(r => r.Target.Id == request.Value.Id).Target;
            var items = new List<NativeContextMenu.Item>();
            OpenTarget selected = null;
            if (target.Path != null)
            {
                string path = target.Path;
                bool exists = File.Exists(path);
                items.Add(new("Open", exists, () => selected = OpenHomeRecent(path)));
                items.Add(new("Show in Folder", exists, () => ShowHomeRecentInFolder(path)));
                items.Add(new("Copy Path", Action: () => CopyHomeText(path)));
                items.Add(new("Remove from Recent", Action: () => RemoveHomeRecent(path)));
            }
            else
            {
                string id = target.SessionId;
                items.Add(new("Open Session", id != null && HomeSessionAvailable(id), () => selected = OpenHomeSession(id)));
                items.Add(new("Copy Session Information", Action: () => CopyHomeText(target.Information)));
            }
            // The native modal loop consumes dismissal input and provides keyboard navigation.
            try
            {
                NativeContextMenu.Show(_window.Handle, request.Value.X, request.Value.Y, items, input);
                chosen = selected;
            }
            finally
            {
                _homeContextRouter.Reset();
                input.ReleaseAll();
                _homeContextClickRevision = input.ClickRevision;
            }
            return true;
        }

        private OpenTarget OpenHomeRecent(string path)
        {
            if (File.Exists(path)) { return OpenTarget.File(path); }
            SetMessage($"{System.IO.Path.GetFileName(path)} was not found. REMOVE FROM LIST takes it off.", error: true);
            _infos.Remove(path);
            return null;
        }

        private void ShowHomeRecentInFolder(string path)
        {
            if (File.Exists(path)) { ShowInFolder(path); }
            else { OpenHomeRecent(path); }
        }

        private void RemoveHomeRecent(string path)
        {
            _recent.Remove(path);
            _infos.Remove(path);
            if (_thumbnails.Remove(path, out uint texture) && texture != 0) { Gl.DeleteTexture(texture); }
            _homeContextRouter.Reset();
        }

        private static bool HomeSessionAvailable(string id)
        {
            SessionInfo session = LiveSessions.ReadInfo(id);
            return session != null && session.Protocol <= LiveProtocol.VERSION && session.IsAlive();
        }

        private OpenTarget OpenHomeSession(string id)
        {
            if (HomeSessionAvailable(id)) { return OpenTarget.Live(id); }
            SetMessage("This session is no longer running in Revit. Open the model there and press Go.", error: true);
            return null;
        }

        private void CopyHomeText(string text)
        {
            try
            {
                System.Windows.Forms.Clipboard.SetText(text ?? string.Empty, System.Windows.Forms.TextDataFormat.UnicodeText);
                SetMessage("Copied to clipboard", error: false);
            }
            catch (Exception ex)
            {
                Utilities.Log_Utils.Write($"Home clipboard failed: {ex.Message}");
                SetMessage($"Could not copy to clipboard: {ex.Message}", error: true);
            }
        }

        private static string SessionInformation(SessionInfo session) =>
            $"Model: {session.DocTitle}\r\nPath: {session.DocPath}\r\nSession: {session.SessionId}\r\nRevit: {session.RevitVersion}\r\nState: {session.State}\r\nSnapshot: {session.SnapshotNumber}\r\nExisting phase: {session.ExistingPhaseName}\r\nPhase: {session.PhaseName}";
    }
}
