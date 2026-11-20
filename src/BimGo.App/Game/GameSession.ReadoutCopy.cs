using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using BimGo.Platform;
using BimGo.Scene;

namespace BimGo.Game
{
    internal sealed partial class GameSession
    {
        internal void CopyScanInformation(InputState input, int element, int dynamicId)
        {
            if ((uint)element >= (uint)Scene.Elements.Length) { return; }
            if (!CopyCommandAvailable(input, InputAction.ScanCopyInfo, InputCatalog.SIT_SCAN)) { return; }

            ElementRecord record = Scene.Elements[element];
            BimGo.Physics.DynamicInstance instance = Dynamics.Find(dynamicId);
            string elementId = instance == null ? record.ElementId.ToString(System.Globalization.CultureInfo.InvariantCulture)
                : instance.RevitId > 0 ? $"{instance.RevitId}{(instance.IsClone ? " (clone)" : " (moved)")}"
                : !instance.Committed ? "clone (not committed)"
                : EditsGoToRevit ? "creating in Revit…" : "clone #" + instance.CloneKey;
            string group = CategoryCatalog.GROUP_NAMES[(int)CategoryCatalog.All[record.CategoryIndex].Group];
            if (record.IsProxy) { group += " (proxy)"; }
            string phase = instance != null && instance.IsClone ? "New work (clone)"
                : record.IsLinked && record.Phase == PhaseRole.Existing ? "Existing" : record.Phase switch
                {
                    PhaseRole.New => "New work",
                    PhaseRole.Between => "Built between phases",
                    PhaseRole.Unphased => "Not phased",
                    _ => Scene.ExistingPhaseName == null ? "Existing" : "Existing (" + Scene.ExistingPhaseName + ")"
                };
            var text = new StringBuilder()
                .AppendLine(record.Name)
                .AppendLine($"Element ID: {elementId}")
                .AppendLine($"Unique ID: {record.UniqueId}")
                .AppendLine($"Category: {record.CategoryName}")
                .AppendLine($"Family / type: {record.FamilyType}")
                .AppendLine($"Level: {record.LevelName}")
                .AppendLine($"Group: {group}")
                .AppendLine($"Phase: {phase}");
            if (instance != null && instance.RevitId > 0 && instance.RevitId.ToString(System.Globalization.CultureInfo.InvariantCulture) != record.ElementId.ToString(System.Globalization.CultureInfo.InvariantCulture))
            { text.AppendLine($"Source element ID: {record.ElementId}"); }
            if (Scene.LinkOf(record) is LinkInfo link) { text.AppendLine($"Source model: {link.Label}"); }
            else { text.AppendLine($"Source model: {Scene.Provenance?.ModelTitle ?? Scene.ModelTitle ?? "host"}"); }

            if (instance != null)
            {
                if (instance.RevitId > 0) { text.AppendLine($"Runtime element ID: {instance.RevitId}"); }
                else if (!instance.Committed) { text.AppendLine("Runtime state: clone (not committed)"); }
                else { text.AppendLine($"Runtime clone: {instance.CloneKey}"); }
            }

            ParameterTable parameters = Scene.Parameters;
            int count = Math.Min(parameters.CountFor(element), 8);
            for (int i = 0; i < count; i++)
            {
                if (parameters.TryGet(element, i, out string name, out string value))
                {
                    text.AppendLine().Append(name).Append(": ").Append(value ?? string.Empty);
                }
            }
            CopyReadoutText(input, text.ToString(), "Element information copied");
        }

        internal void CopyMeasurementInformation(InputState input, string text)
        {
            if (!CopyCommandAvailable(input, InputAction.MeasureCopyInfo, InputCatalog.SIT_MEASURE)) { return; }
            if (string.IsNullOrWhiteSpace(text))
            {
                input.ConsumeFrame();
                Toast("No current measurement to copy", 2f);
                return;
            }
            CopyReadoutText(input, text ?? string.Empty, "Measurement copied");
        }

        internal void CopyScanValue(InputState input, string label, string value)
        {
            if (!CopyValueAvailability.IsAvailable(value))
            {
                input.ConsumeFrame();
                Toast("This value is empty", 2f);
                return;
            }
            CopyReadoutText(input, value ?? string.Empty, label + " copied");
        }

        internal void ShowScanValueMenu(InputState input, int x, int y, string label, string value)
        {
            CancelReviewPointerGesture(); // The accepted RMB belongs to this menu, never Scan's secondary action.
            input.ConsumeFrame();
            NativeContextMenu.Show(_window.Handle, x, y,
                new[] { new NativeContextMenu.Item("Copy Value", Enabled: CopyValueAvailability.IsAvailable(value),
                    Action: () => CopyScanValue(input, label, value)) }, input);
        }

        private bool CopyCommandAvailable(InputState input, InputAction action, string situation)
        {
            InputAction conflict = InputCatalog.PressedCommandConflict(Hotkeys.Map, action, input, situation);
            if (conflict == InputAction.None) { return true; }
            Toast($"{InputCatalog.Get(action).Label} suppressed: shortcut conflicts with {InputCatalog.Get(conflict).Label}. Resolve red bindings in CONTROLS.",
                4f, important: true);
            return false;
        }

        private void CopyReadoutText(InputState input, string text, string notice)
        {
            input.ConsumeFrame();
            if (!CopyValueAvailability.IsAvailable(text))
            {
                Toast("No value to copy", 2f);
                return;
            }
            try
            {
                Clipboard.SetText(text, TextDataFormat.UnicodeText);
                Toast(notice, 2f);
            }
            catch (Exception ex) when (ex is ExternalException or ThreadStateException)
            {
                Toast("Clipboard unavailable; information was not copied", 3f);
            }
        }
    }
}
