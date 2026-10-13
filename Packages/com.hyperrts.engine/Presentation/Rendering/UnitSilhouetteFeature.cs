using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace HyperRTS.Presentation.Rendering
{
    /// <summary>Draws a team-colour silhouette of every unit on the units rendering layer where cover hides it.</summary>
    public sealed class UnitSilhouetteFeature : ScriptableRendererFeature
    {
        [Tooltip("Rendering layer (0-31) that unit renderers carry; only they get silhouettes.")]
        [SerializeField, Range(0, 31)] private int renderingLayer = UnitRenderingLayer.Index;

        private UnitSilhouettePass pass;

        public int RenderingLayer => renderingLayer;

        public override void Create() => pass = new UnitSilhouettePass(UnitRenderingLayer.Mask(renderingLayer));

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
