using BimGo.Platform;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BimGo.Tests
{
    [TestClass]
    public sealed class CopyValueAvailabilityTests
    {
        [TestMethod]
        public void EmptyVisibleReadoutsCannotBeCopied()
        {
            Assert.IsFalse(CopyValueAvailability.IsAvailable(null));
            Assert.IsFalse(CopyValueAvailability.IsAvailable(string.Empty));
            Assert.IsTrue(CopyValueAvailability.IsAvailable("0"));
            Assert.IsTrue(CopyValueAvailability.IsAvailable("—"));
        }
    }
}
