using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace HyperRTS.Presentation.Rendering
{
    /// <summary>
    /// Copies URP's screen-space shadow texture with the contact trace applied and registers the copy under the same
    /// global, so the opaques that follow read it.
    /// </summary>
    public sealed class ContactShadowsPass : ScriptableRenderPass
    {
        private static readonly int ScreenShadowsId = Shader.PropertyToID("_ScreenSpaceShadowmapTexture");
        private static readonly int ProjectionId = Shader.PropertyToID("_ContactShadowProjection");
        private static readonly int InverseProjectionId = Shader.PropertyToID("_ContactShadowInverseProjection");
        private static readonly int SunId = Shader.PropertyToID("_ContactShadowSun");
        private static readonly int TraceId = Shader.PropertyToID("_ContactShadowTrace");
        private static readonly int ScreenId = Shader.PropertyToID("_ContactShadowScreen");

        private readonly Material material;
        private ContactShadowsFeature settings;
        private Vector3 sunForward;
        private float sunShadowStrength;

        private sealed class PassData
        {
            public Material Material;
        }

        public ContactShadowsPass(Material material)
        {
            this.material = material;
            profilingSampler = new ProfilingSampler("Contact Shadows");
        }

        // Right after URP's Screen Space Shadows pass, which forward renderers place at AfterRenderingPrePasses + 1.
        public void Setup(ContactShadowsFeature settings, in VisibleLight sun)
        {
            this.settings = settings;
            sunForward = sun.localToWorldMatrix.GetColumn(2);
            sunShadowStrength = sun.light.shadowStrength;
            renderPassEvent = RenderPassEvent.AfterRenderingPrePasses + 2;
            ConfigureInput(ScriptableRenderPassInput.Depth);
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var cameraData = frameData.Get<UniversalCameraData>();
            var resources = frameData.Get<UniversalResourceData>();
            var desc = cameraData.cameraTargetDescriptor;
            desc.depthStencilFormat = GraphicsFormat.None;
            desc.msaaSamples = 1;
            desc.graphicsFormat = GraphicsFormat.R8_UNorm;
            var shadows = UniversalRenderer.CreateRenderGraphTexture(renderGraph, desc, "_ContactShadowedScreenShadows", false);
            Publish(cameraData, desc);

            using var builder = renderGraph.AddRasterRenderPass<PassData>("Contact Shadows", out var data, profilingSampler);
            data.Material = material;
            builder.UseGlobalTexture(ScreenShadowsId);
            builder.UseTexture(resources.cameraDepthTexture);
            builder.SetRenderAttachment(shadows, 0, AccessFlags.WriteAll);
            builder.SetGlobalTextureAfterPass(shadows, ScreenShadowsId);
            builder.SetRenderFunc(static (PassData pass, RasterGraphContext context) =>
                Blitter.BlitTexture(context.cmd, Vector2.one, pass.Material, 0));
        }

        // The projection is the unflipped one, since the depth and shadow textures are both intermediate textures.
        private void Publish(UniversalCameraData cameraData, RenderTextureDescriptor desc)
        {
            var projection = GL.GetGPUProjectionMatrix(cameraData.GetProjectionMatrix(), false);
            var sun = cameraData.GetViewMatrix().MultiplyVector(-sunForward).normalized;
            material.SetMatrix(ProjectionId, projection);
            material.SetMatrix(InverseProjectionId, projection.inverse);
            material.SetVector(SunId, sun);
            material.SetVector(TraceId, new Vector4(settings.length, settings.steps, settings.thickness,
                settings.strength * sunShadowStrength));
            material.SetVector(ScreenId, new Vector4(desc.width, desc.height, 1f / desc.width, 1f / desc.height));
        }
    }
}
