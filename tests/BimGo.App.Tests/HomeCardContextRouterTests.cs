using BimGo.Platform;
using BimGo.Shell;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BimGo.Tests
{
    [TestClass]
    public sealed class HomeCardContextRouterTests
    {
        private static readonly HomeCardContextRouter.Row[] Rows =
        {
            new("recent:A", 10, 10, 100, 80),
            new("live:B", 10, 100, 100, 60)
        };

        private static PointerEvent Event(PointerEventKind kind, int x = 20, int y = 20) => new(kind, 1, x, y);
        private static HomeCardContextRouter.Request? Route(HomeCardContextRouter router, params PointerEvent[] events) =>
            router.Process(Rows, events, 8, 8);

        [TestMethod]
        public void MenuUsesReleaseTargetAndPositionNotLaterCursorMove()
        {
            var router = new HomeCardContextRouter();
            HomeCardContextRouter.Request? request = Route(router, Event(PointerEventKind.RightDown),
                Event(PointerEventKind.RightUp, 21, 22), Event(PointerEventKind.Move, 25, 120));
            Assert.IsNotNull(request);
            Assert.AreEqual(new HomeCardContextRouter.Request("recent:A", 21, 22), request.Value);
            Assert.IsNull(Route(router, Event(PointerEventKind.RightUp)));
        }

        [TestMethod]
        public void HeldRightButtonDoesNotOpenUntilReleaseAcrossFrames()
        {
            var router = new HomeCardContextRouter();
            Assert.IsNull(Route(router, Event(PointerEventKind.RightDown, 20, 120)));
            Assert.IsNull(Route(router));
            Assert.AreEqual("live:B", Route(router, Event(PointerEventKind.RightUp, 20, 120))?.Id);
        }

        [TestMethod]
        public void DragAwayThenBackNeverOpensMenu()
        {
            var router = new HomeCardContextRouter();
            Assert.IsNull(Route(router, Event(PointerEventKind.RightDown), Event(PointerEventKind.Move, 24, 20),
                Event(PointerEventKind.Move), Event(PointerEventKind.RightUp)));
        }

        [TestMethod]
        public void DifferentRowBlankSpaceAndLeftClickCannotRetarget()
        {
            var router = new HomeCardContextRouter();
            Assert.IsNull(Route(router, Event(PointerEventKind.RightDown), Event(PointerEventKind.RightUp, 20, 120)));
            Assert.IsNull(Route(router, Event(PointerEventKind.RightDown, 0, 0), Event(PointerEventKind.RightUp)));
            Assert.IsNull(Route(router, Event(PointerEventKind.RightDown), Event(PointerEventKind.Down), Event(PointerEventKind.RightUp)));
            Assert.IsNull(Route(router, Event(PointerEventKind.Down), Event(PointerEventKind.Up)));
        }

        [TestMethod]
        public void CancellationAndOwnerResetPreventDelayedMenu()
        {
            var router = new HomeCardContextRouter();
            Assert.IsNull(Route(router, Event(PointerEventKind.RightDown), Event(PointerEventKind.Cancel), Event(PointerEventKind.RightUp)));
            Route(router, Event(PointerEventKind.RightDown));
            router.Reset(); // Home lost focus, refreshed, scrolled, or left for a model/modal.
            Assert.IsNull(Route(router, Event(PointerEventKind.RightUp)));
        }

        [TestMethod]
        public void ChangedLayoutOrIdentityCancelsHeldTarget()
        {
            var router = new HomeCardContextRouter();
            Route(router, Event(PointerEventKind.RightDown));
            Assert.IsNull(router.Process(new[] { new HomeCardContextRouter.Row("recent:A", 11, 10, 100, 80) },
                new[] { Event(PointerEventKind.RightUp) }, 8, 8));
            Route(router, Event(PointerEventKind.RightDown));
            Assert.IsNull(router.Process(new[] { new HomeCardContextRouter.Row("recent:C", 10, 10, 100, 80) },
                new[] { Event(PointerEventKind.RightUp) }, 8, 8));
        }

        [TestMethod]
        public void RowEdgesAndGapsAreNotTargets()
        {
            var router = new HomeCardContextRouter();
            Assert.IsNull(Route(router, Event(PointerEventKind.RightDown, 110, 20), Event(PointerEventKind.RightUp, 110, 20)));
            Assert.IsNull(Route(router, Event(PointerEventKind.RightDown, 20, 90), Event(PointerEventKind.RightUp, 20, 90)));
        }
    }
}
