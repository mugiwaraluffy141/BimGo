using BimGo.Clashes;

namespace BimGo.Reports
{
    /// <summary>Report-only values captured before export; no live document references or review mutations.</summary>
    internal sealed record ClashExcelRow(string ClashId, string Test, string Group, string Status, string AssignedTo, string Notes,
        long ElementAId, string ModelA, string CategoryA, string FamilyTypeA, string LevelA,
        long ElementBId, string ModelB, string CategoryB, string FamilyTypeB, string LevelB,
        string Grid, double X, double Y, double Z, double Distance, bool Crossing);

    internal sealed record ClashExcelSnapshot(string ModelTitle, string Scope, DateTime CreatedUtc, IReadOnlyList<ClashExcelRow> Rows)
    {
        public const string UNITS = "m";

        public static ClashExcelSnapshot Capture(ClashDocument document, IReadOnlyList<ClashRecord> clashes,
            string modelTitle, string scope, DateTime createdUtc,
            Func<ClashRecord, string> gridLocation = null, Func<string, string> linkLabel = null)
        {
            if (document == null) { throw new ArgumentNullException(nameof(document)); }
            if (clashes == null) { throw new ArgumentNullException(nameof(clashes)); }
            var groups = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (ClashGroup group in document.Groups ?? Enumerable.Empty<ClashGroup>())
            {
                if (group?.ClashIds == null) { continue; }
                foreach (string id in group.ClashIds)
                {
                    if (!string.IsNullOrEmpty(id)) { groups.TryAdd(id, ClashGroups.DisplayName(group)); }
                }
            }
            string Source(ClashElementRef element) => element == null ? string.Empty : string.IsNullOrEmpty(element.Link)
                ? modelTitle ?? string.Empty : linkLabel?.Invoke(element.Link) ?? element.Link;
            var rows = new ClashExcelRow[clashes.Count];
            for (int i = 0; i < rows.Length; i++)
            {
                ClashRecord clash = clashes[i] ?? throw new ArgumentException("A report cannot contain a missing clash.", nameof(clashes));
                rows[i] = new ClashExcelRow(clash.Id, document.FindTest(clash.TestId)?.Name ?? string.Empty,
                    groups.TryGetValue(clash.Id, out string group) ? group : string.Empty,
                    clash.Status ?? string.Empty, clash.AssignedTo ?? string.Empty, clash.Notes ?? string.Empty,
                    clash.A?.ElementId ?? 0, Source(clash.A), clash.A?.Category ?? string.Empty, clash.A?.FamilyType ?? string.Empty, clash.A?.Level ?? string.Empty,
                    clash.B?.ElementId ?? 0, Source(clash.B), clash.B?.Category ?? string.Empty, clash.B?.FamilyType ?? string.Empty, clash.B?.Level ?? string.Empty,
                    gridLocation?.Invoke(clash) ?? string.Empty, clash.X, clash.Y, clash.Z, clash.Distance, clash.Crossing);
            }
            return new ClashExcelSnapshot(modelTitle ?? string.Empty, scope ?? string.Empty, createdUtc, Array.AsReadOnly(rows));
        }
    }
}
