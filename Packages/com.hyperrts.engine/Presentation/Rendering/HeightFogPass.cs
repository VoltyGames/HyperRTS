using HyperRTS.Presentation.Environment;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace HyperRTS.Presentation.Rendering
{
    /// <summary>Publishes the camera's blended <see cref="HeightFog"/> and blends it over the opaques and sky.</summary>
    public sealed class HeightFogPass : ScriptableRenderPass
    {
        private readonly Material material;
        private HeightFog fog;

        // Whether this camera draws the fog, else it only clears the globals for its transparents.
        private bool drawn;

        private sealed class PassData
        {
            public Material Material;
            public HeightFog Fog;
        }

        // After the skybox and screen-space decals so they fog too, and before the unit silhouettes so those stay crisp.
        public HeightFogPass(Material material)
        {
            this.material = material;
            renderPassEvent = RenderPassEvent.AfterRenderingSkybox + 1;
            profilingSampler = new ProfilingSampler("Height Fog");
        }

        public void Setup(HeightFog fog, bool drawn)
        {
            this.fog = fog;
            this.drawn = drawn;
            ConfigureInput(drawn ? ScriptableRenderPassInput.Depth : ScriptableRenderPassInput.None);
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (drawn)
            {
                RecordDraw(renderGraph, frameData.Get<UniversalResourceData>());
            }
            else
            {
                RecordClear(renderGraph);
            }
        }

        private void RecordDraw(RenderGraph renderGraph, UniversalResourceData resources)
        {
            using var builder = renderGraph.AddRasterRenderPass<PassData>("Height Fog", out var data, profilingSampler);
            data.Material = material;
            data.Fog = fog;
            builder.UseTexture(resources.cameraDepthTexture);
            builder.SetRenderAttachment(resources.activeColorTexture, 0, AccessFlags.ReadWrite);
            builder.AllowGlobalStateModification(true);
            builder.AllowPassCulling(false);
            builder.SetRenderFunc(static (PassData pass, RasterGraphContext context) =>
            {
                pass.Fog.Publish(context.cmd, true);
                context.cmd.DrawProcedural(Matrix4x4.identity, pass.Material, 0, MeshTopology.Triangles, 3);
            });
        }

        // No attachment, so a camera without fog doesn't load and store its colour just to clear the globals.
        private void RecordClear(RenderGraph renderGraph)
        {
            using var builder = renderGraph.AddComputePass<PassData>("Height Fog Clear", out var data, profilingSampler);
            data.Fog = fog;
            builder.AllowGlobalStateModification(true);
            builder.AllowPassCulling(false);
            builder.SetRenderFunc(static (PassData pass, ComputeGraphContext context) => pass.Fog.Publish(context.cmd, false));
        }
    }
}
