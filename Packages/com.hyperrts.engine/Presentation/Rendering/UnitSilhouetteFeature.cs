using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace HyperRTS.Presentation.Rendering
{
    /// <summary>Draws a team-colour silhouette of every unit on the units rendering layer where cover hides it.</summary>
    public sealed class UnitSilhouetteFeature : ScriptableRendererFeature
    {
        private UnitSilhouettePass pass;

        public override void Create() => pass = new UnitSilhouettePass(UnitRenderingLayer.Mask);

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (renderingData.cameraData.cameraType != CameraType.Game)
            {
                return;
            }

            renderer.EnqueuePass(pass);
        }
    }
}
