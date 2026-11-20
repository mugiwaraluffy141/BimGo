using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BimGo.Clashes;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BimGo.Tests
{
    /// <summary>Keep the existing JSON sharing path intact while adding new review UI and report formats.</summary>
    [TestClass]
    public sealed class ClashExchangeRegressionTests
    {
        [TestMethod]
        public void Report_ImportedTwice_PreservesIdentityGroupsHistoryAndVietnameseNotes()
        {
            string folder = Path.Combine(Path.GetTempPath(), "BimGoExchange_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            try
            {
                DateTime updated = new(2026, 11, 19, 8, 0, 0, DateTimeKind.Utc);
                ClashDocument source = SearchSets.Seeded();
                string testId = source.Tests[0].Id;
                var clash = new ClashRecord
                {
                    Id = "shared-clash",
                    TestId = testId,
                    A = new ClashElementRef { UniqueId = "host-element" },
                    B = new ClashElementRef { UniqueId = "linked-element", Link = "structure-instance" },
                    Status = ClashStatus.REVIEWED,
                    AssignedTo = "Nguyễn Văn An",
                    Notes = "Kiểm tra ống gió & kết cấu <tầng 2>",
                    CommentId = "linked-issue",
                    Updated = updated,
                    UpdatedBy = "reviewer",
                    X = 123.5,
                    Y = -45.25,
                    Z = 8,
                    Distance = 0.025,
                    Crossing = true
                };
                source.Clashes.Add(clash);
                source.Groups = new List<ClashGroup>
                {
                    new() { Id = "shared-group", TestId = testId, Name = "Nhóm tầng 2", ClashIds = new List<string> { clash.Id }, Updated = updated }
                };
                source.Runs.Add(new ClashRunRecord { TestId = testId, Utc = updated, Found = 1, New = 1 });
                string path = Path.Combine(folder, "shared.json");
                Assert.IsTrue(ClashFiles.ExportReport(path, source, "Model", "model-key", "BimGo test", out string error), error);

                var target = new ClashDocument();
                ClashImportResult? first = ClashFiles.ImportReport(path, target, "model-key", out error);
                Assert.IsNotNull(first, error);
                Assert.AreEqual(1, first.Value.ClashesAdded);
                Assert.AreEqual(1, first.Value.GroupsAdded);
                Assert.IsTrue(first.Value.SameModel);

                ClashImportResult? second = ClashFiles.ImportReport(path, target, "model-key", out error);
                Assert.IsNotNull(second, error);
                Assert.AreEqual(0, second.Value.ClashesAdded);
                Assert.AreEqual(0, second.Value.ClashesUpdated);
                Assert.AreEqual(0, second.Value.GroupsAdded);
                Assert.AreEqual(source.Sets.Count, target.Sets.Count);
                Assert.AreEqual(source.Tests.Count, target.Tests.Count);
                Assert.AreEqual(1, target.Clashes.Count);
                Assert.AreEqual(1, target.Runs.Count);
                Assert.AreEqual(1, target.Groups.Count);

                // Saving and reopening the received data must not change the shared identities or review metadata.
                ClashDocument reopened = ClashFiles.Parse(ClashFiles.Serialize(target), out error);
                Assert.IsNotNull(reopened, error);
                ClashRecord actual = reopened.Clashes.Single();
                Assert.AreEqual(clash.Id, actual.Id);
                Assert.AreEqual(clash.Key, actual.Key);
                Assert.AreEqual(clash.Status, actual.Status);
                Assert.AreEqual(clash.AssignedTo, actual.AssignedTo);
                Assert.AreEqual(clash.Notes, actual.Notes);
                Assert.AreEqual(clash.CommentId, actual.CommentId);
                Assert.AreEqual(clash.Updated, actual.Updated);
                Assert.AreEqual(clash.X, actual.X);
                Assert.AreEqual(clash.Y, actual.Y);
                Assert.AreEqual(clash.Z, actual.Z);
                Assert.AreEqual(clash.Distance, actual.Distance);
                Assert.AreEqual("shared-group", reopened.Groups.Single().Id);
                CollectionAssert.AreEqual(new[] { clash.Id }, reopened.Groups.Single().ClashIds);
            }
            finally
            {
                File.Delete(Path.Combine(folder, "shared.json"));
                Directory.Delete(folder);
            }
        }
    }
}
