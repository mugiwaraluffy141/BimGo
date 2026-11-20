using System.Text.Json;
using System.Globalization;
using BimGo.Clashes;

namespace BimGo.Platform
{
    /// <summary>Session-local configuration snapshots; clipboard text is never deserialized as trusted configuration.</summary>
    internal sealed class SetupClipboard
    {
        public const string Format = "BimGo.Setup.v1";
        private readonly string _session = Guid.NewGuid().ToString("N");
        private string _payload, _model;
        private ClashDocument _document;
        private ClashTest _test;
        private SearchSet _set;

        public string Copy(ClashDocument document, string model, ClashTest test, SearchSet set)
        {
            _document = document;
            _model = model;
            _test = test == null ? null : CopyTest(test);
            _set = set?.Copy();
            // The opaque token refers only to this in-memory snapshot. Foreign/tampered clipboard
            // content cannot inject rules, IDs, results, history or issue links.
            _payload = JsonSerializer.Serialize(new { Version = 1, Model = model, Session = _session,
                Kind = test != null ? "Test" : "Set", Token = Guid.NewGuid().ToString("N") });
            return _payload;
        }

        public bool CanPaste(string payload, ClashDocument document, string model, bool tests)
        {
            if (_payload == null || !string.Equals(_payload, payload, StringComparison.Ordinal)
                || !ReferenceEquals(_document, document) || !string.Equals(_model, model, StringComparison.Ordinal)) { return false; }
            if (!tests) { return _set != null; }
            return _test != null && document.FindSet(_test.SetA) != null
                && (string.IsNullOrEmpty(_test.SetB) || document.FindSet(_test.SetB) != null);
        }

        public ClashTest PasteTest(string payload, ClashDocument document, string model)
        {
            if (!CanPaste(payload, document, model, tests: true)) { return null; }
            ClashTest copy = CopyTest(_test);
            copy.Name = CopyName(_test.Name, document.Tests.Select(t => t.Name));
            return copy;
        }

        public SearchSet PasteSet(string payload, ClashDocument document, string model)
        {
            if (!CanPaste(payload, document, model, tests: false)) { return null; }
            SearchSet copy = _set.Copy(newId: true);
            copy.Name = CopyName(_set.Name, document.Sets.Select(s => s.Name));
            return copy;
        }

        private static ClashTest CopyTest(ClashTest test) => new()
        {
            Name = test.Name, Kind = test.Kind, SetA = test.SetA, SetB = test.SetB,
            Tolerance = test.Tolerance, Enabled = test.Enabled
        };

        private static string CopyName(string name, IEnumerable<string> existing)
        {
            var names = new HashSet<string>(existing, StringComparer.OrdinalIgnoreCase);
            string candidate = BoundedName(name, " (copy)");
            for (int i = 2; names.Contains(candidate); i++) { candidate = BoundedName(name, $" (copy {i})"); }
            return candidate;
        }

        private static string BoundedName(string name, string suffix)
        {
            const int MAX_NAME = 60;
            name ??= string.Empty;
            int budget = MAX_NAME - suffix.Length;
            int length = 0;
            TextElementEnumerator elements = StringInfo.GetTextElementEnumerator(name);
            while (elements.MoveNext())
            {
                int end = elements.ElementIndex + elements.GetTextElement().Length;
                if (end > budget) { break; }
                length = end;
            }
            return name.Substring(0, length) + suffix;
        }
    }
}
