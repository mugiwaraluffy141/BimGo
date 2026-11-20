using BimGo.Platform;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BimGo.Tests
{
    [TestClass]
    public sealed class ContextClickTrackerTests
    {
        [TestMethod]
        public void SameTargetReleaseProducesOnlyOneContextClick()
        {
            var tracker = new ContextClickTracker();
            tracker.Begin("test:A", 10, 10);
            Assert.IsTrue(tracker.End("test:A", 11, 10, 8, 8));
            Assert.IsFalse(tracker.End("test:A", 11, 10, 8, 8));
        }

        [TestMethod]
        public void DownOnOneRowAndUpOnAnotherNeverRetargets()
        {
            var tracker = new ContextClickTracker();
            tracker.Begin("test:A", 10, 10);
            Assert.IsFalse(tracker.End("test:B", 10, 11, 8, 8));
            tracker.Begin(null, 10, 10);
            Assert.IsFalse(tracker.End("test:B", 10, 11, 8, 8));
        }

        [TestMethod]
        public void MoveAwayThenBackRemainsCancelledWithinOneBatch()
        {
            var tracker = new ContextClickTracker();
            tracker.Begin("set:A", 10, 10);
            tracker.Move(14, 10, 8, 8);
            tracker.Move(10, 10, 8, 8);
            Assert.IsFalse(tracker.End("set:A", 10, 10, 8, 8));
        }

        [TestMethod]
        public void ClashRowDragCannotOpenContextMenuEvenWhenReleasedOnTheSameRow()
        {
            var tracker = new ContextClickTracker();
            tracker.Begin("clash:A", 10, 10);
            tracker.Move(14, 10, 8, 8);
            Assert.IsFalse(tracker.End("clash:A", 10, 10, 8, 8));
        }

        [TestMethod]
        public void OrderedRoutingPreservesConsumedDownAndRejectsDifferentReleaseRow()
        {
            var events = new[]
            {
                new PointerEvent(PointerEventKind.RightDown, 1, 10, 10),
                new PointerEvent(PointerEventKind.Move, 2, 30, 10),
                new PointerEvent(PointerEventKind.RightUp, 3, 30, 10)
            };
            var batch = new PointerEventBatch(events);
            var tracker = new ContextClickTracker();
            bool opened = tracker.Process(batch, e => e.X < 20 ? "clash:A" : "clash:B", 8, 8,
                enabled: true, out _, out _);

            Assert.IsFalse(opened);
            Assert.IsTrue(batch.Handled[0]); // Dock down is consumed even when the gesture later cancels.
            Assert.IsFalse(batch.Handled[2]);
            batch.MarkHandled(0, false); // Later row routing must not overwrite the consumed context down.
            Assert.IsTrue(batch.Handled[0]);
        }

        [TestMethod]
        public void LaterViewportRightDownCancelsEarlierDockMenuInSameBatch()
        {
            var batch = new PointerEventBatch(new[]
            {
                new PointerEvent(PointerEventKind.RightDown, 1, 10, 10),
                new PointerEvent(PointerEventKind.RightUp, 2, 10, 10),
                new PointerEvent(PointerEventKind.RightDown, 3, 50, 10)
            });
            var tracker = new ContextClickTracker();
            bool opened = tracker.Process(batch, e => e.X < 20 ? "clash:A" : null, 8, 8,
                enabled: true, out string target, out _);

            Assert.IsFalse(opened);
            Assert.IsNull(target);
            Assert.IsTrue(batch.Handled[0]);
            Assert.IsTrue(batch.Handled[1]);
            Assert.IsFalse(batch.Handled[2]);
        }

        [TestMethod]
        public void ScrollingBatchCannotRearmOrCompleteContextClick()
        {
            var tracker = new ContextClickTracker();
            tracker.Begin("clash:A", 10, 10);
            var scrolling = new PointerEventBatch(new[]
            {
                new PointerEvent(PointerEventKind.RightDown, 1, 10, 10),
                new PointerEvent(PointerEventKind.RightUp, 2, 10, 10)
            });
            Assert.IsFalse(tracker.Process(scrolling, _ => "clash:A", 8, 8, enabled: false, out _, out _));
            Assert.IsFalse(scrolling.Handled[0]);

            var laterRelease = new PointerEventBatch(new[] { new PointerEvent(PointerEventKind.RightUp, 3, 10, 10) });
            Assert.IsFalse(tracker.Process(laterRelease, _ => "clash:A", 8, 8, enabled: true, out _, out _));
        }

        [TestMethod]
        public void LastStillHeldNativeDownOwnsDockLookDecision()
        {
            var events = new[]
            {
                new PointerEvent(PointerEventKind.RightDown, 1, 110, 110),
                new PointerEvent(PointerEventKind.RightUp, 2, 110, 110),
                new PointerEvent(PointerEventKind.RightDown, 3, 10, 10)
            };
            Assert.IsFalse(PointerEventOwnership.RightButtonOwnedByRect(false, rightDown: true, rightPressed: true,
                sampledDownInside: true, events: events, left: 100, top: 100, width: 100, height: 100));
            Assert.IsTrue(PointerEventOwnership.RightButtonOwnedByRect(false, rightDown: true, rightPressed: true,
                sampledDownInside: true, events: Array.Empty<PointerEvent>(), left: 100, top: 100, width: 100, height: 100));
        }

        [TestMethod]
        public void FocusScrollOrModalResetInvalidatesPendingTarget()
        {
            var tracker = new ContextClickTracker();
            tracker.Begin("set:A", 10, 10);
            tracker.Reset();
            Assert.IsFalse(tracker.End("set:A", 10, 10, 8, 8));
        }
    }
}
