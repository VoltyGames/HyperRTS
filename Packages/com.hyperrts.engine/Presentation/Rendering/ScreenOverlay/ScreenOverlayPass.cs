using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace HyperRTS.Presentation.Rendering.ScreenOverlay
{
    /// <summary>Draws one LightMode into the camera's final target with the camera's unjittered matrices.</summary>
    public sealed class ScreenOverlayPass : ScriptableRenderPass
    {
        private static readonly int ProjectionParamsId = Shader.PropertyToID("_ProjectionParams");

        private readonly ShaderTagId _lightMode;

        public ScreenOverlayPass(string lightMode)
        {
            _lightMode = new ShaderTagId(lightMode);
            profilingSampler = new ProfilingSampler("Screen Overlay");
            // After URP's final post and blit, so at the screen's size whatever the upscaler, and before it draws the UI;
            // one step after the team outlines, so health bars draw over their lines.
            renderPassEvent = RenderPassEvent.AfterRendering + 1;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var cameraData = frameData.Get<UniversalCameraData>();
            if (!cameraData.resolveFinalTarget)
            {
                return;
            }

            var renderingData = frameData.Get<UniversalRenderingData>();
            var drawing = RenderingUtils.CreateDrawingSettings(_lightMode, renderingData, cameraData,
                frameData.Get<UniversalLightData>(), SortingCriteria.CommonTransparent);
            var renderers = new RendererListParams(renderingData.cullResults, drawing, new FilteringSettings(RenderQueueRange.all));

            using var builder = renderGraph.AddRasterRenderPass<PassData>("Screen Overlay", out var data, profilingSampler);
            data.Renderers = renderGraph.CreateRendererList(renderers);
            data.Target = frameData.Get<UniversalResourceData>().activeColorTexture;
            data.Camera = cameraData.camera;
            data.View = cameraData.GetViewMatrix();
            builder.UseRendererList(data.Renderers);
            builder.SetRenderAttachment(data.Target, 0);
            builder.AllowGlobalStateModification(true);
            builder.SetRenderFunc(static (PassData data, RasterGraphContext context) => Draw(data, context));
        }

        // URP set the matrices for its own textures at the frame's start: jittered for the upscaler, and flipped where
        // the final target may not be. Jitter would shake shapes drawn at the screen's size, so take the camera's own.
        private static void Draw(PassData data, RasterGraphContext context)
        {
            var flipped = SystemInfo.graphicsUVStartsAtTop &&
                          context.GetTextureUVOrigin(data.Target) == TextureUVOrigin.BottomLeft;
            var projection = GL.GetGPUProjectionMatrix(data.Camera.projectionMatrix, flipped);
            RenderingUtils.SetViewAndProjectionMatrices(context.cmd, data.View, projection, false);
            var far = data.Camera.farClipPlane;
            context.cmd.SetGlobalVector(ProjectionParamsId,
                new Vector4(flipped ? -1f : 1f, data.Camera.nearClipPlane, far, 1f / far));
            context.cmd.DrawRendererList(data.Renderers);
        }

        private sealed class PassData
        {
            public RendererListHandle Renderers;
            public TextureHandle Target;
            public Camera Camera;
            public Matrix4x4 View;
        }
    }
}
