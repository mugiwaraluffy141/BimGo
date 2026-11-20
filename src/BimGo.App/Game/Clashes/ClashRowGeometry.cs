using System.Numerics;

namespace BimGo.Game
{
    /// <summary>Geometry shared by row rendering and ordered native-event hit testing.</summary>
    internal static class ClashRowGeometry
    {
        public static Vector2 PencilCenter(float x, float y, float width, float thumbnailWidth, float scale, bool pictures, bool group)
        {
            if (!pictures) { return new Vector2(x + width - 20f * scale, y + 32f * scale); }
            return group
                ? new Vector2(x + thumbnailWidth + 8f * scale, y + 23f * scale)
                : new Vector2(x + thumbnailWidth - 4f * scale, y + 17f * scale);
        }
    }
}
