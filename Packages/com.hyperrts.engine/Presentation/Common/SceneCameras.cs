using UnityEngine;

namespace HyperRTS.Presentation.Common
{
    /// <summary>Which cameras draw what a scene's renderers submit themselves before each camera renders.</summary>
    public static class SceneCameras
    {
        /// <summary>
        /// Game and Scene view cameras that see the owner's layer and scene. Reflection probes and asset preview
        /// cameras are skipped: they render other scenes or bake once, and would rebuild per-camera state for nothing.
        /// </summary>
        public static bool Draws(Camera camera, GameObject owner)
        {
            var kind = camera.cameraType is CameraType.Game or CameraType.SceneView;
            var seesLayer = (camera.cullingMask & (1 << owner.layer)) != 0;
            var ownScene = !camera.scene.IsValid() || camera.scene == owner.scene;
            return kind && seesLayer && ownScene;
        }
    }
}
