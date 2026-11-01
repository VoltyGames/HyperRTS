using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace HyperRTS.Presentation.Fog
{
    /// <summary>Copies the camera colour through the fog of war over the opaques, from the camera depth texture.</summary>
    public sealed class FogOfWarPass : ScriptableRenderPass
    {
        private readonly Material _material;

        private sealed class PassData
        {
            public Material Material;
            public TextureHandle Source;
        }

        // After URP's fog and before every transparent, so health bars, markers and the placement ghost stay clear.
        public FogOfWarPass(Material material)
        {
            _material = material;
            renderPassEvent = RenderPassEvent.BeforeRenderingTransparents;
            profilingSampler = new ProfilingSampler("Fog Of War");
            ConfigureInput(ScriptableRenderPassInput.Depth);
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var resources = frameData.Get<UniversalResourceData>();
            // Reading the colour needs a copy; the back buffer can't be one.
            if (resources.isActiveTargetBackBuffer)
            {
                return;
            }

            var source = resources.activeColorTexture;
            var description = renderGraph.GetTextureDesc(source);
            description.name = "Fog Of War";
            description.clearBuffer = false;
            var destination = renderGraph.CreateTexture(description);
            using (var builder = renderGraph.AddRasterRenderPass<PassData>("Fog Of War", out var data, profilingSampler))
            {
                data.Material = _material;
                data.Source = source;
                builder.UseTexture(source);
                builder.UseTexture(resources.cameraDepthTexture);
                builder.SetRenderAttachment(destination, 0, AccessFlags.Write);
                builder.SetRenderFunc(static (PassData pass, RasterGraphContext context) =>
                    Blitter.BlitTexture(context.cmd, pass.Source, new Vector4(1f, 1f, 0f, 0f), pass.Material, 0));
            }

            resources.cameraColor = destination;
        }
    }
}
