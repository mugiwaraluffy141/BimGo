using BimGo.Platform;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BimGo.Tests
{
    [TestClass]
    public sealed class NativeTextEditorTests
    {
        [TestMethod]
        public void Limit_NormalizesNewlinesBeforeApplyingBudget()
        {
            Assert.AreEqual("a b c d", NativeTextEditor.Limit("a\r\nb\rc\nd", 7, multiline: false));
            Assert.AreEqual("a\nb\nc\nd", NativeTextEditor.Limit("a\r\nb\rc\nd", 7, multiline: true));
        }

        [TestMethod]
        public void Limit_DoesNotSplitSurrogatePairOrCombiningSequence()
        {
            Assert.AreEqual("A", NativeTextEditor.Limit("A😀B", 2, multiline: false));
            Assert.AreEqual("A😀", NativeTextEditor.Limit("A😀B", 3, multiline: false));
            Assert.AreEqual("", NativeTextEditor.Limit("a\u0301b", 1, multiline: false));
            Assert.AreEqual("a\u0301", NativeTextEditor.Limit("a\u0301b", 2, multiline: false));
        }

        [TestMethod]
        public void Limit_PreservesWholeEmojiSequenceAndVietnameseWithinBudget()
        {
            const string family = "👨‍👩‍👧‍👦";
            Assert.AreEqual("", NativeTextEditor.Limit(family + "X", family.Length - 1, multiline: false));
            Assert.AreEqual(family, NativeTextEditor.Limit(family + "X", family.Length, multiline: false));
            const string vietnamese = "Kiểm tra ống gió";
            Assert.AreEqual(vietnamese, NativeTextEditor.Limit(vietnamese, vietnamese.Length, multiline: false));
        }

        [TestMethod]
        public void Limit_HandlesEmptyAndZeroBudget()
        {
            Assert.AreEqual("", NativeTextEditor.Limit(null, 10, multiline: false));
            Assert.AreEqual("", NativeTextEditor.Limit("", 0, multiline: true));
            Assert.AreEqual("", NativeTextEditor.Limit("Text", 0, multiline: false));
        }
    }
}
