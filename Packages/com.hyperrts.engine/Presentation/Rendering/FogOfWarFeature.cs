using HyperRTS.Presentation.Fog;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace HyperRTS.Presentation.Rendering
{
    /// <summary>Draws the <see cref="FogOfWarRenderer"/> grid full screen in game cameras while a match shows it.</summary>
    public sealed class FogOfWarFeature : FullScreenFogFeature<FogOfWarPass>
    {
        public const string ShaderName = "Hidden/HyperRTS/FogOfWar";

        protected override string RequiredShaderName => ShaderName;

        protected override FogOfWarPass CreatePass(Material material) => new(material);

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            var cameraData = renderingData.cameraData;
            var drawn = cameraData.cameraType == CameraType.Game && !IsTopDown(cameraData.camera);
            if (Pass == null || !drawn || !FogOfWarRenderer.Shown)
            {
                return;
            }

            renderer.EnqueuePass(Pass);
        }
    }
}
