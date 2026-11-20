using BimGo.Platform;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BimGo.Tests
{
    [TestClass]
    public sealed class ClashReviewContextTests
    {
        [TestMethod]
        public void ContextFocusDoesNotReplaceBulkCurrentAndCanTargetSelectedGroup()
        {
            var context = new ClashReviewContext();
            var state = new ClashReviewContext.SelectionState(new object(), "test", "bulk-current", null, "anchor", 4);
            context.Focus(null, "right-clicked-result", state);
            Assert.IsTrue(context.TryGet(state, out string group, out string clash));
            Assert.IsNull(group);
            Assert.AreEqual("right-clicked-result", clash);
            Assert.AreEqual("bulk-current", state.CurrentClashId);
            context.Focus("right-clicked-group", null, state);
            Assert.IsTrue(context.TryGet(state, out group, out clash));
            Assert.AreEqual("right-clicked-group", group);
            Assert.IsNull(clash);
        }

        [TestMethod]
        public void NavigationSelectionTestModelAndExplicitRebuildInvalidateFocus()
        {
            var context = new ClashReviewContext();
            var state = new ClashReviewContext.SelectionState(new object(), "test", "current", null, "anchor", 4);
            foreach (ClashReviewContext.SelectionState changed in new[]
            {
                state with { Document = new object() }, state with { TestId = "other" },
                state with { CurrentClashId = "other" }, state with { CurrentGroupId = "group" },
                state with { AnchorId = "other" }, state with { SelectedCount = 1 }
            })
            {
                context.Focus(null, "right-clicked", state);
                Assert.IsFalse(context.TryGet(changed, out _, out _));
                Assert.IsFalse(context.TryGet(state, out _, out _));
            }
            context.Focus("group", null, state);
            context.Reset();
            Assert.IsFalse(context.TryGet(state, out _, out _));
        }

        [TestMethod]
        public void SourceSnapshotsSurviveRetargetingAndPreserveMergeDestinationOrder()
        {
            var selected = new List<string> { "clash-a", "clash-b" };
            // Existing PickedClashGroups puts an unpicked destination first; already-picked
            // destinations retain original click order. Freeze must preserve both contracts.
            foreach (string[] orderedGroups in new[]
            {
                new[] { "unpicked-destination", "source-a", "source-b" },
                new[] { "source-a", "picked-destination", "source-b" }
            })
            {
                selected.Clear();
                selected.AddRange(new[] { "clash-a", "clash-b" });
                var picks = orderedGroups.ToList();
                ClashReviewContext.MenuSources frozen = ClashReviewContext.FreezeSources(selected, picks);
                selected.Clear(); // ClickClashGroupRow(false) retargets selection/picks.
                picks.Clear();
                selected.Add("destination-member");
                CollectionAssert.AreEqual(new[] { "clash-a", "clash-b" }, frozen.ClashIds);
                CollectionAssert.AreEqual(orderedGroups, frozen.GroupIds);
            }
        }
    }
}
