using System.Numerics;
using BimGo.Game;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BimGo.Tests
{
    [TestClass]
    public sealed class ClashRowGeometryTests
    {
        [TestMethod]
        [DataRow(1f)]
        [DataRow(1.5f)]
        [DataRow(2f)]
        public void GroupPicturePencilMatchesStackedThumbnailGeometry(float scale)
        {
            float x = 100, y = 200, thumb = 160 * scale;
            float imageX = x + 4 * scale + 26 * scale;
            float imageWidth = thumb - 8 * scale;
            Vector2 expected = new(imageX + imageWidth - 14 * scale, y + 3 * scale + 6 * scale + 14 * scale);
            Assert.AreEqual(expected, ClashRowGeometry.PencilCenter(x, y, 400 * scale, thumb, scale, pictures: true, group: true));
            Assert.AreEqual(x + thumb + 8 * scale, expected.X);
        }

        [TestMethod]
        public void ResultPictureAndIndentedTextRowsKeepTheirOriginalPencilPositions()
        {
            Assert.AreEqual(new Vector2(256, 217), ClashRowGeometry.PencilCenter(100, 200, 400, 160, 1, pictures: true, group: false));
            Assert.AreEqual(new Vector2(480, 232), ClashRowGeometry.PencilCenter(114, 200, 386, 160, 1, pictures: false, group: false));
            Assert.AreEqual(new Vector2(480, 232), ClashRowGeometry.PencilCenter(100, 200, 400, 160, 1, pictures: false, group: true));
        }
    }
}
