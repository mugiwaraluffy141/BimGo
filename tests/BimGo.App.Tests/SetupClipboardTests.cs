using BimGo.Clashes;
using BimGo.Platform;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BimGo.Tests
{
    [TestClass]
    public sealed class SetupClipboardTests
    {
        private static ClashDocument Document()
        {
            var document = new ClashDocument();
            document.Sets.Add(new SearchSet { Name = "Ống nước" });
            document.Tests.Add(new ClashTest { Name = "Kiểm tra", SetA = document.Sets[0].Id,
                LastRunUtc = DateTime.UtcNow, LastRunModel = "previous", LastRunTruncated = true });
            return document;
        }

        [TestMethod]
        public void TestSnapshotHasFreshIdUniqueNameAndNoRunHistory()
        {
            ClashDocument doc = Document();
            var clipboard = new SetupClipboard();
            string payload = clipboard.Copy(doc, "model", doc.Tests[0], null);
            doc.Tests[0].Name = "Changed after copy";
            ClashTest copy = clipboard.PasteTest(payload, doc, "model");
            Assert.AreNotEqual(doc.Tests[0].Id, copy.Id);
            Assert.AreEqual("Kiểm tra (copy)", copy.Name);
            Assert.AreEqual(doc.Sets[0].Id, copy.SetA);
            Assert.IsNull(copy.LastRunUtc);
            Assert.IsNull(copy.LastRunModel);
            Assert.IsFalse(copy.LastRunTruncated);
            doc.Tests.Add(copy);
            ClashTest second = clipboard.PasteTest(payload, doc, "model");
            Assert.AreEqual("Kiểm tra (copy 2)", second.Name);
            Assert.AreNotEqual(copy.Id, second.Id);
            Assert.AreEqual(0, doc.Clashes.Count);
            Assert.AreEqual(2, doc.Tests.Count); // helper never mutates document
        }

        [TestMethod]
        public void RejectsForeignModelSessionDocumentKindAndTampering()
        {
            ClashDocument doc = Document();
            var clipboard = new SetupClipboard();
            string payload = clipboard.Copy(doc, "model", doc.Tests[0], null);
            Assert.IsFalse(clipboard.CanPaste(payload, doc, "other", true));
            Assert.IsFalse(clipboard.CanPaste(payload, Document(), "model", true));
            Assert.IsFalse(new SetupClipboard().CanPaste(payload, doc, "model", true));
            Assert.IsFalse(clipboard.CanPaste(payload + " ", doc, "model", true));
            Assert.IsFalse(clipboard.CanPaste("arbitrary text", doc, "model", true));
            Assert.IsFalse(clipboard.CanPaste(null, doc, "model", true));
            Assert.IsFalse(clipboard.CanPaste(payload, doc, "model", false));
            Assert.IsNull(clipboard.PasteSet(payload, doc, "model"));
        }

        [TestMethod]
        public void TestPasteRequiresBothReferencedSetsStillExist()
        {
            ClashDocument doc = Document();
            var second = new SearchSet();
            doc.Sets.Add(second);
            doc.Tests[0].SetB = second.Id;
            var clipboard = new SetupClipboard();
            string payload = clipboard.Copy(doc, null, doc.Tests[0], null);
            Assert.IsTrue(clipboard.CanPaste(payload, doc, null, true));
            doc.Sets.Remove(second);
            Assert.IsNull(clipboard.PasteTest(payload, doc, null));
            doc.Sets.Add(second);
            doc.Sets.RemoveAt(0);
            Assert.IsNull(clipboard.PasteTest(payload, doc, null));
        }

        [TestMethod]
        public void SetSnapshotAndEachPasteAreDeepCopies()
        {
            ClashDocument doc = Document();
            SearchSet original = doc.Sets[0];
            original.Rules.Add(new SetRule { Field = RuleField.CATALOG, Values = new() { "Original" } });
            original.Groups = new List<RuleGroup> { new() { Rules = new List<SetRule> { new() { Values = new() { "Nested" } } } } };
            var clipboard = new SetupClipboard();
            string payload = clipboard.Copy(doc, "model", null, original);
            original.Rules[0].Values[0] = "Changed";
            original.Groups[0].Rules[0].Values[0] = "Changed";
            SearchSet first = clipboard.PasteSet(payload, doc, "model");
            Assert.AreNotEqual(original.Id, first.Id);
            Assert.AreEqual("Ống nước (copy)", first.Name);
            Assert.AreEqual("Original", first.Rules[0].ValueAt(0));
            Assert.AreEqual("Nested", first.Groups[0].Rules[0].ValueAt(0));
            first.Rules[0].Values[0] = "Edited paste";
            doc.Sets.Add(first);
            SearchSet next = clipboard.PasteSet(payload, doc, "model");
            Assert.AreEqual("Original", next.Rules[0].ValueAt(0));
            Assert.AreEqual("Ống nước (copy 2)", next.Name);
            Assert.AreNotEqual(first.Id, next.Id);
        }

        [TestMethod]
        public void NewCopyInvalidatesOldPayloadAndCarriesVersionModelSession()
        {
            ClashDocument doc = Document();
            var clipboard = new SetupClipboard();
            string old = clipboard.Copy(doc, "model", doc.Tests[0], null);
            string current = clipboard.Copy(doc, "model", null, doc.Sets[0]);
            Assert.IsFalse(clipboard.CanPaste(old, doc, "model", true));
            Assert.IsTrue(clipboard.CanPaste(current, doc, "model", false));
            StringAssert.Contains(current, "\"Version\":1");
            StringAssert.Contains(current, "\"Model\":\"model\"");
            StringAssert.Contains(current, "\"Session\":");
        }

        [TestMethod]
        public void SixtyCharacterNamesStayBoundedAndUniqueAfterSuffixCollisions()
        {
            ClashDocument doc = Document();
            doc.Tests[0].Name = new string('a', 60);
            doc.Sets[0].Name = new string('b', 60);
            var clipboard = new SetupClipboard();
            string payload = clipboard.Copy(doc, "model", doc.Tests[0], null);
            ClashTest first = clipboard.PasteTest(payload, doc, "model");
            Assert.AreEqual(new string('a', 53) + " (copy)", first.Name);
            Assert.AreEqual(60, first.Name.Length);
            doc.Tests.Add(first);
            ClashTest next = clipboard.PasteTest(payload, doc, "model");
            Assert.AreEqual(new string('a', 51) + " (copy 2)", next.Name);
            doc.Tests.Add(next);
            Assert.AreEqual(new string('a', 51) + " (copy 3)", clipboard.PasteTest(payload, doc, "model").Name);

            payload = clipboard.Copy(doc, "model", null, doc.Sets[0]);
            SearchSet set = clipboard.PasteSet(payload, doc, "model");
            Assert.AreEqual(new string('b', 53) + " (copy)", set.Name);
            doc.Sets.Add(set);
            Assert.AreEqual(new string('b', 51) + " (copy 2)", clipboard.PasteSet(payload, doc, "model").Name);
        }

        [TestMethod]
        public void NameTruncationDoesNotSplitSurrogatesCombiningMarksOrEmojiSequences()
        {
            foreach (string element in new[] { "😀", "e\u0301", "👩‍💻" })
            {
                ClashDocument doc = Document();
                // The 53-unit root budget lands inside this text element.
                doc.Tests[0].Name = new string('x', 52) + element + new string('y', 60 - 52 - element.Length);
                var clipboard = new SetupClipboard();
                string payload = clipboard.Copy(doc, "model", doc.Tests[0], null);
                ClashTest copy = clipboard.PasteTest(payload, doc, "model");
                Assert.AreEqual(new string('x', 52) + " (copy)", copy.Name);
                Assert.IsTrue(copy.Name.Length <= 60);
            }
        }
    }
}
