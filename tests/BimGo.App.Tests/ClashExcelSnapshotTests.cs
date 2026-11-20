using BimGo.Clashes;
using BimGo.Reports;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BimGo.Tests
{
    [TestClass]
    public sealed class ClashExcelSnapshotTests
    {
        [TestMethod]
        public void CapturesOnlyRequestedRowsInOrderAndKeepsReviewStatusIndependent()
        {
            var doc = new ClashDocument();
            var test = new ClashTest { Name = "Ống gió" };
            doc.Tests.Add(test);
            var a = new ClashRecord { Id = "a", TestId = test.Id, Status = ClashStatus.RESOLVED, X = 123.5, Distance = 0.025,
                A = new ClashElementRef { ElementId = 101, Category = "Ducts" },
                B = new ClashElementRef { ElementId = 102, Link = "link-uid" } };
            var b = new ClashRecord { Id = "b", TestId = test.Id, Notes = "=Giữ nguyên & kiểm tra" };
            doc.Clashes.AddRange(new[] { a, b, new ClashRecord { Id = "not-requested" } });
            doc.Groups = new() { new ClashGroup { Name = "Nhóm 1", TestId = test.Id, ClashIds = new() { a.Id, b.Id } } };
            var snapshot = ClashExcelSnapshot.Capture(doc, new[] { b, a }, "Host", "Selection", DateTime.UtcNow,
                _ => "C-4", _ => "Structure.rvt");
            CollectionAssert.AreEqual(new[] { "b", "a" }, snapshot.Rows.Select(r => r.ClashId).ToArray());
            Assert.AreEqual("=Giữ nguyên & kiểm tra", snapshot.Rows[0].Notes);
            Assert.AreEqual(ClashStatus.RESOLVED, snapshot.Rows[1].Status);
            Assert.AreEqual("Host", snapshot.Rows[1].ModelA);
            Assert.AreEqual("Structure.rvt", snapshot.Rows[1].ModelB);
            Assert.AreEqual("Nhóm 1", snapshot.Rows[1].Group);
            Assert.AreEqual("C-4", snapshot.Rows[1].Grid);
            Assert.AreEqual(123.5, snapshot.Rows[1].X);
            Assert.AreEqual(0.025, snapshot.Rows[1].Distance);
            Assert.AreEqual(ClashStatus.RESOLVED, a.Status);
        }

        [TestMethod]
        public void LaterDocumentEditsDoNotChangeCapturedValues()
        {
            var doc = new ClashDocument();
            var test = new ClashTest { Name = "Original" };
            doc.Tests.Add(test);
            var clash = new ClashRecord { TestId = test.Id, Notes = "Before", A = new ClashElementRef { ElementId = 12 } };
            var snapshot = ClashExcelSnapshot.Capture(doc, new[] { clash }, "Model", "Shown", DateTime.UtcNow);
            test.Name = "Changed";
            clash.Notes = "After";
            clash.A.ElementId = 99;
            Assert.AreEqual("Original", snapshot.Rows[0].Test);
            Assert.AreEqual("Before", snapshot.Rows[0].Notes);
            Assert.AreEqual(12L, snapshot.Rows[0].ElementAId);
        }

        [TestMethod]
        public void EmptyScopeStaysEmptyAndUnknownOptionalValuesStayBlank()
        {
            var doc = new ClashDocument();
            Assert.AreEqual(0, ClashExcelSnapshot.Capture(doc, Array.Empty<ClashRecord>(), null, null, DateTime.UtcNow).Rows.Count);
            var row = ClashExcelSnapshot.Capture(doc, new[] { new ClashRecord() }, "Model", "Shown", DateTime.UtcNow).Rows.Single();
            Assert.AreEqual("", row.Test);
            Assert.AreEqual("", row.Group);
            Assert.AreEqual("", row.Grid);
        }

        [TestMethod]
        public void MissingElementDescriptorsRemainBlankRatherThanFabricatingHostData()
        {
            var doc = new ClashDocument();
            doc.Groups = new() { new ClashGroup { ClashIds = null } };
            var row = ClashExcelSnapshot.Capture(doc, new[] { new ClashRecord { A = null, B = null } }, "Host", "Shown", DateTime.UtcNow).Rows.Single();
            Assert.AreEqual(0L, row.ElementAId);
            Assert.AreEqual("", row.ModelA);
            Assert.AreEqual("", row.CategoryA);
            Assert.AreEqual("", row.FamilyTypeB);
            Assert.AreEqual("", row.LevelB);
        }
    }
}
