using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace HyperRTS.Presentation.Rendering
{
    /// <summary>Copies the camera colour through the sharpen shader and makes the copy the camera colour.</summary>
    public sealed class SharpenPass : ScriptableRenderPass
    {
        private static readonly int StrengthId = Shader.PropertyToID("_SharpenStrength");

        private readonly Material _material;

        public SharpenPass(Material material)
        {
            _material = material;
            profilingSampler = new ProfilingSampler("Sharpen");
            renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
            // The frame must stay in a texture past post-processing for the pass to read it.
            requiresIntermediateTexture = true;
        }

        public void Setup(float strength) => _material.SetFloat(StrengthId, strength);

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var resources = frameData.Get<UniversalResourceData>();
            if (resources.isActiveTargetBackBuffer)
            {
                return;
            }

            var source = resources.activeColorTexture;
            var desc = renderGraph.GetTextureDesc(source);
            desc.name = "_SharpenedColor";
            desc.clearBuffer = false;
            var sharpened = renderGraph.CreateTexture(desc);
            renderGraph.AddBlitPass(new RenderGraphUtils.BlitMaterialParameters(source, sharpened, _material, 0), "Sharpen");
            resources.cameraColor = sharpened;
        }
    }
}
