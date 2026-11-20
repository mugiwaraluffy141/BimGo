using BimGo.Platform;
using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BimGo.Tests
{
    [TestClass]
    public sealed class DoubleClickTrackerTests
    {
        private static (List<string> Actions, PointerEventBatch Batch) RouteBatch(params PointerEvent[] events)
        {
            var tracker = new DoubleClickTracker();
            var batch = new PointerEventBatch(events);
            var actions = new List<string>();
            string selected = null;
            for (int i = 0; i < batch.Count; i++)
            {
                PointerEvent e = batch.Events[i];
                string target = e.X switch { 10 => "clash-a", 20 => "group", 30 => "pencil", 40 => "clash-b", _ => null };
                bool row = target == "clash-a" || target == "clash-b";
                Action press = target == null ? null : () => { selected = target; actions.Add("press:" + target); };
                bool routed = tracker.Route(e, row ? target : null, press, () => actions.Add("revit:" + selected), 500, 6, 6, 4, 4);
                batch.Handled[i] = routed;
            }
            return (actions, batch);
        }

        [TestMethod]
        public void QueuedRowThenGroupRoutesBothWithoutLegacyReplay()
        {
            var (actions, batch) = RouteBatch(new(PointerEventKind.Down, 100, 10, 10), new(PointerEventKind.Up, 110, 10, 10),
                new(PointerEventKind.Down, 200, 20, 10), new(PointerEventKind.Up, 210, 20, 10));
            CollectionAssert.AreEqual(new[] { "press:clash-a", "press:group" }, actions);
            Assert.IsNull(batch.PendingPress);
        }

        [TestMethod]
        public void QueuedRowThenPencilRoutesBothWithoutLegacyReplay()
        {
            var (actions, batch) = RouteBatch(new(PointerEventKind.Down, 100, 10, 10), new(PointerEventKind.Up, 110, 10, 10),
                new(PointerEventKind.Down, 200, 30, 10), new(PointerEventKind.Up, 210, 30, 10));
            CollectionAssert.AreEqual(new[] { "press:clash-a", "press:pencil" }, actions);
            Assert.IsNull(batch.PendingPress);
        }

        [TestMethod]
        public void QueuedDoubleClashThenOtherRowSendsOriginalTargetExactlyOnce()
        {
            var (actions, batch) = RouteBatch(new(PointerEventKind.Down, 100, 10, 10), new(PointerEventKind.Up, 110, 10, 10),
                new(PointerEventKind.Down, 200, 10, 10), new(PointerEventKind.Up, 210, 10, 10),
                new(PointerEventKind.Down, 300, 40, 10), new(PointerEventKind.Up, 310, 40, 10));
            CollectionAssert.AreEqual(new[] { "press:clash-a", "press:clash-a", "revit:clash-a", "press:clash-b" }, actions);
            Assert.IsNull(batch.PendingPress);
        }

        [TestMethod]
        public void LaterUnhandledPressKeepsOriginalPosition_NotFinalMouseMove()
        {
            var (actions, batch) = RouteBatch(new(PointerEventKind.Down, 100, 10, 10), new(PointerEventKind.Up, 110, 10, 10),
                new(PointerEventKind.Down, 200, 99, 12), new(PointerEventKind.Move, 205, 40, 10));
            CollectionAssert.AreEqual(new[] { "press:clash-a" }, actions);
            Assert.IsNotNull(batch.PendingPress);
            Assert.AreEqual(99, batch.PendingPress.Value.X);
            Assert.AreEqual(12, batch.PendingPress.Value.Y);
            Assert.IsFalse(batch.Handled[2]);
            Assert.IsFalse(batch.Handled[3]);
        }

        [TestMethod]
        public void HandledRowPressCannotReplayAtGroupUnderFinalMouseMove()
        {
            var (actions, batch) = RouteBatch(new(PointerEventKind.Down, 100, 10, 10), new(PointerEventKind.Move, 105, 20, 10));
            CollectionAssert.AreEqual(new[] { "press:clash-a" }, actions);
            Assert.IsNull(batch.PendingPress);
        }

        [TestMethod]
        public void UnhandledPressCannotBecomeClashPressWhenCursorMovesOntoRow()
        {
            var (actions, batch) = RouteBatch(new(PointerEventKind.Down, 100, 99, 12), new(PointerEventKind.Move, 105, 10, 10));
            Assert.AreEqual(0, actions.Count);
            Assert.AreEqual(99, batch.PendingPress.Value.X);
            Assert.AreEqual(12, batch.PendingPress.Value.Y);
        }

        [TestMethod]
        public void RightGestureRetainsDownOriginReleaseAndModifiersInOrder()
        {
            var down = new PointerEvent(PointerEventKind.RightDown, 100, 10, 12, Control: true, Alt: true);
            var move = new PointerEvent(PointerEventKind.Move, 105, 40, 12, Control: true, Alt: true);
            var up = new PointerEvent(PointerEventKind.RightUp, 110, 40, 12, Control: true, Alt: true);
            var (actions, batch) = RouteBatch(down, move, up);
            Assert.AreEqual(0, actions.Count);
            Assert.AreEqual(3, batch.Count);
            Assert.AreEqual(down, batch.Events[0]);
            Assert.AreEqual(move, batch.Events[1]);
            Assert.AreEqual(up, batch.Events[2]);
            for (int i = 0; i < batch.Count; i++) { Assert.IsFalse(batch.Handled[i]); }
            Assert.AreEqual(down, batch.PendingRightPress.Value);
            Assert.AreEqual(down, batch.PendingMousePress.Value);
            Assert.IsNull(batch.PendingPress);
            batch.Handled[0] = batch.Handled[2] = true; // setup owns just its right-button gesture
            Assert.IsNull(batch.PendingRightPress);
            Assert.IsFalse(batch.Handled[1]); // movement remains available to another consumer
        }

        [TestMethod]
        public void RightButtonCannotActivateLeftDoubleClickAndBreaksPendingPair()
        {
            var tracker = new DoubleClickTracker();
            Click(tracker, 100);
            Assert.IsFalse(Send(tracker, PointerEventKind.RightDown, 150));
            Assert.IsFalse(Send(tracker, PointerEventKind.RightUp, 160));
            Click(tracker, 200);
        }

        [TestMethod]
        public void ConsumingLeftPressPreservesLaterRightPressAtItsNativeOrigin()
        {
            var (actions, batch) = RouteBatch(new(PointerEventKind.Down, 100, 10, 10), new(PointerEventKind.Up, 110, 10, 10),
                new(PointerEventKind.RightDown, 200, 99, 12), new(PointerEventKind.Move, 205, 40, 10),
                new(PointerEventKind.RightUp, 210, 40, 10));
            CollectionAssert.AreEqual(new[] { "press:clash-a" }, actions);
            Assert.IsNull(batch.PendingPress);
            Assert.AreEqual(99, batch.PendingRightPress.Value.X);
            Assert.AreEqual(99, batch.PendingMousePress.Value.X);
            Assert.IsFalse(batch.Handled[2]);
            Assert.IsFalse(batch.Handled[4]);
        }

        [TestMethod]
        public void ReusableBatchClearsMasksAndIgnoresStaleCapacityAfterSmallerCapture()
        {
            var batch = new PointerEventBatch();
            PointerEvent[] events = { new(PointerEventKind.Down, 100, 10, 10), new(PointerEventKind.RightDown, 200, 40, 10) };
            batch.Capture(events);
            PointerEvent[] storage = batch.Events;
            bool[] masks = batch.Handled;
            batch.Handled[0] = batch.Handled[1] = true;
            batch.Capture(new[] { events[0] });
            Assert.AreSame(storage, batch.Events);
            Assert.AreSame(masks, batch.Handled);
            Assert.AreEqual(1, batch.Count);
            Assert.IsFalse(batch.Handled[0]);
            Assert.AreEqual(events[0], batch.PendingPress.Value);
            Assert.AreEqual(events[0], batch.PendingMousePress.Value);
            Assert.IsNull(batch.PendingRightPress); // stale right press outside Count must not leak
            batch.Capture(Array.Empty<PointerEvent>());
            Assert.AreEqual(0, batch.Count);
            Assert.IsNull(batch.PendingPress);
            Assert.IsNull(batch.PendingRightPress);
            Assert.IsNull(batch.PendingMousePress);
            batch.Capture(events);
            Assert.IsFalse(batch.Handled[0]);
            Assert.IsFalse(batch.Handled[1]);
            Assert.AreSame(storage, batch.Events);
        }

        [TestMethod]
        public void WarmReusableMovementBatchAndDirectTrackerProcessingAllocateNothing()
        {
            var batch = new PointerEventBatch();
            var tracker = new DoubleClickTracker();
            PointerEvent[] events = { new(PointerEventKind.Move, 100, 10, 10), new(PointerEventKind.Move, 110, 40, 10) };
            // Warm buffer capacity and JIT before measuring the production-style direct processing path.
            for (int frame = 0; frame < 100; frame++) { ProcessMovementFrame(batch, tracker, events); }
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int frame = 0; frame < 1000; frame++) { ProcessMovementFrame(batch, tracker, events); }
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.AreEqual(0L, allocated);
        }

        private static void ProcessMovementFrame(PointerEventBatch batch, DoubleClickTracker tracker, PointerEvent[] events)
        {
            batch.Capture(events);
            for (int i = 0; i < batch.Count; i++)
            {
                batch.Handled[i] = tracker.Process(batch.Events[i], null, 500, 6, 6, 4, 4);
            }
            _ = batch.PendingPress;
            _ = batch.PendingRightPress;
            _ = batch.PendingMousePress;
        }

        private static bool Send(DoubleClickTracker tracker, PointerEventKind kind, uint time, string id = "a", int x = 10, int y = 10,
            bool control = false, bool shift = false, bool alt = false) =>
            tracker.Process(new PointerEvent(kind, time, x, y, control, shift, alt), id, 500, 6, 6, 4, 4);

        private static void Click(DoubleClickTracker tracker, uint time, string id = "a", int x = 10, int y = 10)
        {
            Assert.IsFalse(Send(tracker, PointerEventKind.Down, time, id, x, y));
            Assert.IsFalse(Send(tracker, PointerEventKind.Up, time + 10, id, x, y));
        }

        [TestMethod]
        public void SameIdCompletedPairFiresExactlyOnce_OrderedInOneFrame()
        {
            var tracker = new DoubleClickTracker();
            Click(tracker, 100);
            Assert.IsFalse(Send(tracker, PointerEventKind.Down, 200));
            Assert.IsTrue(Send(tracker, PointerEventKind.Up, 210));
            Click(tracker, 300); // a triple click is not two double clicks
        }

        [TestMethod]
        public void DifferentIdsAndNonRowBreakPair()
        {
            var tracker = new DoubleClickTracker();
            Click(tracker, 100);
            Click(tracker, 200, "b");
            Send(tracker, PointerEventKind.Down, 250, null);
            Click(tracker, 300, "b");
        }

        [TestMethod]
        public void WindowsTimeAndRectangleBoundaries()
        {
            var tracker = new DoubleClickTracker();
            Click(tracker, 100);
            Send(tracker, PointerEventKind.Down, 600, x: 12, y: 8);
            Assert.IsTrue(Send(tracker, PointerEventKind.Up, 610, x: 12, y: 8));
            Click(tracker, 700);
            Click(tracker, 1201); // expired
            Click(tracker, 1300, x: 13); // right edge is outside
        }

        [TestMethod]
        public void ModifierOrDragCannotActivate()
        {
            foreach (int modifier in new[] { 0, 1, 2 })
            {
                var tracker = new DoubleClickTracker();
                Click(tracker, 100);
                Send(tracker, PointerEventKind.Down, 200, control: modifier == 0, shift: modifier == 1, alt: modifier == 2);
                Assert.IsFalse(Send(tracker, PointerEventKind.Up, 210));
                Click(tracker, 300);
            }
            var drag = new DoubleClickTracker();
            Click(drag, 100);
            Send(drag, PointerEventKind.Down, 200);
            Send(drag, PointerEventKind.Move, 205, x: 30);
            Assert.IsFalse(Send(drag, PointerEventKind.Up, 210)); // returning to origin does not hide drag
            Click(drag, 300);
        }

        [TestMethod]
        public void ResetAndCancelInvalidatePendingClick()
        {
            var tracker = new DoubleClickTracker();
            Click(tracker, 100);
            tracker.Reset(); // focus/filter/test/scroll/modal lifecycle
            Click(tracker, 200);
            Send(tracker, PointerEventKind.Cancel, 250);
            Click(tracker, 300);
        }

        [TestMethod]
        public void MessageTimeWrapIsUnsigned()
        {
            var tracker = new DoubleClickTracker();
            Click(tracker, uint.MaxValue - 100);
            Send(tracker, PointerEventKind.Down, 50);
            Assert.IsTrue(Send(tracker, PointerEventKind.Up, 60));
        }

        [TestMethod]
        public void BothPressesRequireReleaseOnSameRow_AndFirstDragIsRejected()
        {
            var tracker = new DoubleClickTracker();
            Send(tracker, PointerEventKind.Down, 100);
            Assert.IsFalse(Send(tracker, PointerEventKind.Up, 110, "b"));
            Click(tracker, 200);
            Send(tracker, PointerEventKind.Down, 300);
            Assert.IsFalse(Send(tracker, PointerEventKind.Up, 310, x: 12)); // drag rectangle's right edge
            Send(tracker, PointerEventKind.Down, 400);
            Send(tracker, PointerEventKind.Move, 405, x: 30);
            Assert.IsFalse(Send(tracker, PointerEventKind.Up, 410));
            Click(tracker, 450);
        }
    }
}
