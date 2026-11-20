using BimGo.Audio;
using BimGo.Platform;
using BimGo.Reports;

namespace BimGo.Game
{
    internal sealed partial class GameSession
    {
        /// <summary>Capture all Excel report values and count before showing the file dialog.</summary>
        private void StartClashExcelReport()
        {
            if (_clashReportScope is < REPORT_SCOPE_SHOWN or > REPORT_SCOPE_TEST)
            {
                Sound.Play(SoundId.Error);
                Toast("Excel supports shown clashes, selection, or this test only", 3f);
                return;
            }
            try
            {
                var clashes = ClashReportClashes(_clashReportScope);
                if (clashes.Count == 0)
                {
                    Sound.Play(SoundId.Error);
                    Toast("Nothing to report in that scope", 2.5f);
                    return;
                }
                // Keep the stable link instance identity alongside its human-readable source label.
                string SourceLabel(string id)
                {
                    var link = Scene.Links.FirstOrDefault(l => l.InstanceUniqueId == id);
                    return link == null ? id : link.Label + " [" + id + "]";
                }
                var snapshot = ClashExcelSnapshot.Capture(_clashDocument, clashes, Scene.ModelTitle,
                    ClashReportScopeText(_clashReportScope, clashes.Count), DateTime.UtcNow, ClashGridFunction(), SourceLabel);
                _window.SetCaptured(false);
                _window.Input.ReleaseAll();
                string name = SafeFileName(snapshot.ModelTitle) + " clash report " + DateTime.Now.ToString("yyyy-MM-dd HHmm") + ".xlsx";
                string path = FileDialogs.ShowSave(_window.Handle, $"Export Excel report — {snapshot.Rows.Count:N0} clashes",
                    "Excel workbook (*.xlsx)|*.xlsx|All files (*.*)|*.*", SuggestedFolder(), name, ".xlsx");
                _window.Input.ReleaseAll();
                if (path == null) { return; } // Cancellation never creates a file or starts image capture.
                ClashExcelWriter.Write(path, snapshot);
                _clashReportCardOpen = false;
                Sound.Play(SoundId.Commit);
                Toast($"Excel report saved: {Path.GetFileName(path)} ({snapshot.Rows.Count:N0} clashes)", 4f);
            }
            catch (Exception ex)
            {
                Utilities.Log_Utils.Write($"Excel clash report failed: {ex}");
                Sound.Play(SoundId.Error);
                Toast($"The Excel report couldn't be written: {ex.Message}", 5f, important: true);
            }
        }
    }
}
