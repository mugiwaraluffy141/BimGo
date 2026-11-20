namespace BimGo.Platform
{
    /// <summary>Context focus is separate from the bulk selection; menus freeze their source IDs before retargeting.</summary>
    internal sealed class ClashReviewContext
    {
        internal readonly record struct SelectionState(object Document, string TestId, string CurrentClashId,
            string CurrentGroupId, string AnchorId, int SelectedCount);
        internal readonly record struct MenuSources(string[] ClashIds, string[] GroupIds);

        private SelectionState _state;
        private string _groupId, _clashId;

        public void Focus(string groupId, string clashId, SelectionState state)
        {
            _groupId = groupId;
            _clashId = clashId;
            _state = state;
        }

        public bool TryGet(SelectionState state, out string groupId, out string clashId)
        {
            if (state != _state) { Reset(); }
            groupId = _groupId;
            clashId = _clashId;
            return groupId != null || clashId != null;
        }

        public void Reset() { _groupId = _clashId = null; }

        public static MenuSources FreezeSources(IEnumerable<string> clashes, IEnumerable<string> groups) =>
            new(clashes.ToArray(), groups.ToArray());
    }
}
