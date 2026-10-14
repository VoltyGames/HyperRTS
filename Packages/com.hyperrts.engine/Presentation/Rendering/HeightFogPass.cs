using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace HyperRTS.Presentation.Rendering
{
    /// <summary>Blends the current height fog over the opaques and sky, from the camera depth texture.</summary>
    public sealed class HeightFogPass : ScriptableRenderPass
    {
        private readonly Material material;

        private sealed class PassData
        {
            public Material Material;
        }

        // After the skybox and screen-space decals so they fog too, and before the unit silhouettes so those stay crisp.
        public HeightFogPass(Material material)
        {
            this.material = material;
            renderPassEvent = RenderPassEvent.AfterRenderingSkybox + 1;
            profilingSampler = new ProfilingSampler("Height Fog");
            ConfigureInput(ScriptableRenderPassInput.Depth);
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var resources = frameData.Get<UniversalResourceData>();
            using var builder = renderGraph.AddRasterRenderPass<PassData>("Height Fog", out var data, profilingSampler);
            data.Material = material;
            builder.UseTexture(resources.cameraDepthTexture);
            builder.SetRenderAttachment(resources.activeColorTexture, 0, AccessFlags.ReadWrite);
            builder.SetRenderFunc(static (PassData pass, RasterGraphContext context) =>
                context.cmd.DrawProcedural(Matrix4x4.identity, pass.Material, 0, MeshTopology.Triangles, 3));
        }
    }
}
