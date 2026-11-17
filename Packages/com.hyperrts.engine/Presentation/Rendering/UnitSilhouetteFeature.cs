using Unity.Scripting.LifecycleManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace HyperRTS.Presentation.Rendering
{
    /// <summary>Draws a team-colour silhouette of every unit on the units rendering layer where cover hides it.</summary>
    public sealed partial class UnitSilhouetteFeature : ScriptableRendererFeature
    {
        private UnitSilhouettePass _pass;

        /// <summary>Hides every silhouette while set, for footage that should show only the scene.</summary>
        [AutoStaticsCleanup]
        public static bool Hidden { get; set; }

        public override void Create() => _pass = new UnitSilhouettePass(UnitRenderingLayer.Mask);

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (Hidden || renderingData.cameraData.cameraType != CameraType.Game)
            {
                return;
            }

            renderer.EnqueuePass(_pass);
        }
    }
}
