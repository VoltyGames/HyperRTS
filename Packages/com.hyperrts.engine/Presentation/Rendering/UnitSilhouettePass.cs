using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace HyperRTS.Presentation.Rendering
{
    /// <summary>Marks the units' visible pixels in the stencil, then draws their hidden parts in team colour.</summary>
    public sealed class UnitSilhouettePass : ScriptableRenderPass
    {
        private static readonly ShaderTagId MaskTag = new("HyperRTSUnitMask");
        private static readonly ShaderTagId SilhouetteTag = new("HyperRTSSilhouette");

        private readonly uint renderingLayerMask;

        private sealed class PassData
        {
            public RendererListHandle Mask;
            public RendererListHandle Silhouette;
        }

        // After the opaques, skybox and screen-space decals so the silhouette sits on the finished scene, and before
        // the transparents so smoke and effects still cover it.
        public UnitSilhouettePass(uint renderingLayerMask)
        {
            this.renderingLayerMask = renderingLayerMask;
            renderPassEvent = RenderPassEvent.AfterRenderingSkybox;
            profilingSampler = new ProfilingSampler("Unit Silhouettes");
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var resources = frameData.Get<UniversalResourceData>();
            using var builder = renderGraph.AddRasterRenderPass<PassData>("Unit Silhouettes", out var data, profilingSampler);
            data.Mask = RendererList(renderGraph, frameData, MaskTag);
            data.Silhouette = RendererList(renderGraph, frameData, SilhouetteTag);
            builder.UseRendererList(data.Mask);
            builder.UseRendererList(data.Silhouette);
            builder.SetRenderAttachment(resources.activeColorTexture, 0, AccessFlags.Write);
            builder.SetRenderAttachmentDepth(resources.activeDepthTexture, AccessFlags.ReadWrite);
            builder.SetRenderFunc(static (PassData pass, RasterGraphContext context) =>
            {
                context.cmd.DrawRendererList(pass.Mask);
                context.cmd.DrawRendererList(pass.Silhouette);
            });
        }

        private RendererListHandle RendererList(RenderGraph renderGraph, ContextContainer frameData, ShaderTagId tag)
        {
            var rendering = frameData.Get<UniversalRenderingData>();
            var camera = frameData.Get<UniversalCameraData>();
            var lights = frameData.Get<UniversalLightData>();
            var drawing = RenderingUtils.CreateDrawingSettings(tag, rendering, camera, lights, SortingCriteria.CommonOpaque);
            var filtering = new FilteringSettings(RenderQueueRange.opaque, renderingLayerMask: renderingLayerMask);
            return renderGraph.CreateRendererList(new RendererListParams(rendering.cullResults, drawing, filtering));
        }
    }
}
