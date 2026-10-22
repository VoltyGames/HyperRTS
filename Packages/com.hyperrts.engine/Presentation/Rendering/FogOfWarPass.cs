using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace HyperRTS.Presentation.Rendering
{
    /// <summary>Blends the fog of war over the opaques, from the camera depth texture.</summary>
    public sealed class FogOfWarPass : ScriptableRenderPass
    {
        private readonly Material material;

        private sealed class PassData
        {
            public Material Material;
        }

        // After the height fog and before every transparent, so health bars, markers and the placement ghost stay clear.
        public FogOfWarPass(Material material)
        {
            this.material = material;
            renderPassEvent = RenderPassEvent.BeforeRenderingTransparents;
            profilingSampler = new ProfilingSampler("Fog Of War");
            ConfigureInput(ScriptableRenderPassInput.Depth);
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var resources = frameData.Get<UniversalResourceData>();
            using var builder = renderGraph.AddRasterRenderPass<PassData>("Fog Of War", out var data, profilingSampler);
            data.Material = material;
            builder.UseTexture(resources.cameraDepthTexture);
            builder.SetRenderAttachment(resources.activeColorTexture, 0, AccessFlags.ReadWrite);
            builder.SetRenderFunc(static (PassData pass, RasterGraphContext context) =>
                context.cmd.DrawProcedural(Matrix4x4.identity, pass.Material, 0, MeshTopology.Triangles, 3));
        }
    }
}
