using BimGo.Format;
using BimGo.Scene;

namespace BimGo.Game
{
    internal sealed partial class GameSession
    {
        /// <summary>The model's saved camera paths, retained for .bimgo compatibility.</summary>
        public PathStore CameraPaths { get; private set; }

        /// <summary>Loads embedded paths from a .bimgo or paths.json beside a live model.</summary>
        private void InitialisePaths()
        {
            if (IsFileMode)
            {
                CameraPaths = new PathStore(null, Scene.ModelTitle, Scene.OriginOffset);
                CameraPaths.LoadFrom(_options.Document?.Paths);
            }
            else
            {
                CameraPaths = new PathStore(CameraPathFiles.SidecarFor(Scene.CommentsPath), Scene.ModelTitle, Scene.OriginOffset);
                CameraPaths.Load();
            }
            CameraPaths.Changed += UpdateTitle;
        }
    }
}
