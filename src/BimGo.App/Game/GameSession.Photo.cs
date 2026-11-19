using BimGo.Rendering;

namespace BimGo.Game
{
    internal sealed partial class GameSession
    {
        private const long PHOTO_SAMPLE_BUDGET = 40_000_000;

        private RenderTarget _photoTarget, _photoResolve;
        private bool _sharpOff;

        /// <summary>Renders the current view into an off-screen target and reads it back as bottom-up BGRA.</summary>
        private byte[] RenderPhotoView(int width, int height, bool legend = false, int legendScale = 1, bool sharp = true)
        {
            _sharpOff = !sharp;
            try { return RenderPhotoViewSharp(width, height, legend, legendScale); }
            finally { _sharpOff = false; }
        }

        private byte[] RenderPhotoViewSharp(int width, int height, bool legend, int legendScale)
        {
            long pixels = (long)width * height;
            int samples = pixels * 4 <= PHOTO_SAMPLE_BUDGET ? 4 : pixels * 2 <= PHOTO_SAMPLE_BUDGET ? 2 : 0;
            _photoTarget ??= new RenderTarget();
            _photoResolve ??= new RenderTarget();
            RenderScene(width, height, _photoTarget, samples, photo: true);
            if (legend && ProfileLegendInShots)
            {
                int k = Math.Clamp(legendScale, 1, 4);
                _photoTarget.Bind();
                QueueProfileLegendForShot(width / k, height / k);
                _ui.Flush(width / k, height / k);
            }
            _photoResolve.Ensure(width, height, 0);
            _photoTarget.ResolveTo(_photoResolve);
            byte[] bgra = new byte[pixels * 4];
            _photoResolve.ReadPixels(bgra);
            return bgra;
        }
    }
}
