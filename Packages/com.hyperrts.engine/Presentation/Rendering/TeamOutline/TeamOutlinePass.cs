using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace HyperRTS.Presentation.Rendering.TeamOutline
{
    /// <summary>
    /// Draws owned units and buildings in their owner's colour into a mask the size of the camera depth, then lines
    /// the mask's edges into the camera's final target.
    /// </summary>
    public sealed class TeamOutlinePass : ScriptableRenderPass
    {
        private const int HiddenPass = 0;
        private const int OutlinePass = 1;
        private const int MaxReach = 8;

        private static readonly int OutlineId = Shader.PropertyToID("_TeamOutline");
        private static readonly int PixelId = Shader.PropertyToID("_TeamOutlinePixel");
        private static readonly int ProjectionParamsId = Shader.PropertyToID("_ProjectionParams");
        private static readonly ShaderTagId UnitTag = new(TeamOutlineFeature.UnitPass);
        private static readonly ShaderTagId BuildingTag = new(TeamOutlineFeature.BuildingPass);

        private readonly Material _material;

        public TeamOutlinePass(Material material)
        {
            _material = material;
            profilingSampler = new ProfilingSampler("Team Outline");
            // With Screen Overlay, after URP's final blit: at the screen's size whatever the upscaler. Listed before
            // Screen Overlay, so health bars draw over the lines.
            renderPassEvent = RenderPassEvent.AfterRendering;
        }

        public void Setup(float width, float opacity, Vector2Int screen)
        {
            var reach = Mathf.Min(Mathf.CeilToInt(width), MaxReach);
            _material.SetVector(OutlineId, new Vector4(width, reach, opacity, 0f));
            _material.SetVector(PixelId, new Vector4(1f / screen.x, 1f / screen.y, 0f, 0f));
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var cameraData = frameData.Get<UniversalCameraData>();
            if (!cameraData.resolveFinalTarget)
            {
                return;
            }

            var resources = frameData.Get<UniversalResourceData>();
            var depth = resources.cameraDepth;
            var mask = CreateMask(renderGraph, depth);
            RecordMask(renderGraph, frameData, mask, depth);
            RecordOutline(renderGraph, mask, resources.activeColorTexture);
        }

        // The depth's size and samples, so the mask can test against its depth and stencil.
        private static TextureHandle CreateMask(RenderGraph renderGraph, TextureHandle depth)
        {
            var desc = renderGraph.GetTextureDesc(depth);
            desc.name = "_TeamOutlineMask";
            desc.format = GraphicsFormat.R8G8B8A8_SRGB;
            desc.isShadowMap = false;
            desc.bindTextureMS = false;
            desc.enableRandomWrite = false;
            desc.useMipMap = false;
            desc.filterMode = FilterMode.Point;
            desc.clearBuffer = true;
            desc.clearColor = Color.clear;
            return renderGraph.CreateTexture(desc);
        }

        private void RecordMask(RenderGraph renderGraph, ContextContainer frameData, TextureHandle mask, TextureHandle depth)
        {
            var cameraData = frameData.Get<UniversalCameraData>();
            using var builder = renderGraph.AddRasterRenderPass<MaskData>("Team Outline Mask", out var data, profilingSampler);
            data.Units = renderGraph.CreateRendererList(Renderers(frameData, UnitTag, UnitRenderingLayer.Mask));
            data.Buildings = renderGraph.CreateRendererList(Renderers(frameData, BuildingTag, BuildingRenderingLayer.Mask));
            data.Mask = mask;
            data.Material = _material;
            data.Camera = cameraData.camera;
            data.View = cameraData.GetViewMatrix();
            builder.UseRendererList(data.Units);
            builder.UseRendererList(data.Buildings);
            builder.SetRenderAttachment(mask, 0, AccessFlags.Write);
            builder.SetRenderAttachmentDepth(depth, AccessFlags.Read);
            builder.AllowGlobalStateModification(true);
            builder.SetRenderFunc(static (MaskData data, RasterGraphContext context) => DrawMask(data, context));
        }

        private static RendererListParams Renderers(ContextContainer frameData, ShaderTagId pass, uint layer)
        {
            var renderingData = frameData.Get<UniversalRenderingData>();
            var drawing = RenderingUtils.CreateDrawingSettings(pass, renderingData, frameData.Get<UniversalCameraData>(),
                frameData.Get<UniversalLightData>(), SortingCriteria.OptimizeStateChanges);
            var filtering = new FilteringSettings(RenderQueueRange.opaque, renderingLayerMask: layer);
            return new RendererListParams(renderingData.cullResults, drawing, filtering);
        }

        // Unjittered, as Screen Overlay draws: the mask is read after upscaling, where jitter would shake the lines.
        private static void DrawMask(MaskData data, RasterGraphContext context)
        {
            var flipped = SystemInfo.graphicsUVStartsAtTop &&
                          context.GetTextureUVOrigin(data.Mask) == TextureUVOrigin.BottomLeft;
            var projection = GL.GetGPUProjectionMatrix(data.Camera.projectionMatrix, flipped);
            RenderingUtils.SetViewAndProjectionMatrices(context.cmd, data.View, projection, false);
            var far = data.Camera.farClipPlane;
            context.cmd.SetGlobalVector(ProjectionParamsId,
                new Vector4(flipped ? -1f : 1f, data.Camera.nearClipPlane, far, 1f / far));
            context.cmd.DrawRendererList(data.Units);
            context.cmd.DrawRendererList(data.Buildings);
            Blitter.BlitTexture(context.cmd, new Vector4(1f, 1f, 0f, 0f), data.Material, HiddenPass);
        }

        private void RecordOutline(RenderGraph renderGraph, TextureHandle mask, TextureHandle target)
        {
            using var builder = renderGraph.AddRasterRenderPass<OutlineData>("Team Outline", out var data, profilingSampler);
            data.Mask = mask;
            data.Target = target;
            data.Material = _material;
            builder.UseTexture(mask);
            builder.SetRenderAttachment(target, 0);
            builder.SetRenderFunc(static (OutlineData data, RasterGraphContext context) =>
            {
                var flip = context.GetTextureUVOrigin(data.Mask) != context.GetTextureUVOrigin(data.Target);
                var scaleBias = flip ? new Vector4(1f, -1f, 0f, 1f) : new Vector4(1f, 1f, 0f, 0f);
                Blitter.BlitTexture(context.cmd, data.Mask, scaleBias, data.Material, OutlinePass);
            });
        }

        private sealed class MaskData
        {
            public RendererListHandle Units;
            public RendererListHandle Buildings;
            public TextureHandle Mask;
            public Material Material;
            public Camera Camera;
            public Matrix4x4 View;
        }

        private sealed class OutlineData
        {
            public TextureHandle Mask;
            public TextureHandle Target;
            public Material Material;
        }
    }
}
