using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace HyperRTS.Presentation.Rendering
{
    /// <summary>Turns URP's fog off for a camera its fog feature skips, so the camera's surfaces and transparents stay clear.</summary>
    public sealed class ClearAtmosphereFogPass : ScriptableRenderPass
    {
        private static readonly GlobalKeyword SurfaceFog = GlobalKeyword.Create(ShaderKeywordStrings.VolumetricFog);
        private static readonly GlobalKeyword AnalyticFog = GlobalKeyword.Create(ShaderKeywordStrings.FogAnalytic);
        private static readonly GlobalKeyword VolumetricFog = GlobalKeyword.Create(ShaderKeywordStrings.FogVolumetric);
        private static readonly int AnalyticFogEnabledId = Shader.PropertyToID("_AnalyticFogEnabled");
        private static readonly int SunFogDensityId = Shader.PropertyToID("_MainLightFogDensity");

        private sealed class PassData
        {
        }

        // Where URP's own fog globals pass runs, before anything draws.
        public ClearAtmosphereFogPass()
        {
            renderPassEvent = RenderPassEvent.BeforeRenderingShadows;
            profilingSampler = new ProfilingSampler("Clear Atmosphere Fog");
        }

        // No attachment, so the camera doesn't load and store its colour just to set globals.
        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            using var builder = renderGraph.AddComputePass<PassData>("Clear Atmosphere Fog", out _, profilingSampler);
            builder.AllowGlobalStateModification(true);
            builder.AllowPassCulling(false);
            builder.SetRenderFunc(static (PassData _, ComputeGraphContext context) => Clear(context.cmd));
        }

        // URP keeps one of the two fog modes on at all times; analytic with zero density draws nothing.
        private static void Clear(ComputeCommandBuffer cmd)
        {
            cmd.SetKeyword(SurfaceFog, false);
            cmd.SetKeyword(AnalyticFog, true);
            cmd.SetKeyword(VolumetricFog, false);
            cmd.SetGlobalFloat(AnalyticFogEnabledId, 0f);
            cmd.SetGlobalFloat(SunFogDensityId, 0f);
        }
    }
}
