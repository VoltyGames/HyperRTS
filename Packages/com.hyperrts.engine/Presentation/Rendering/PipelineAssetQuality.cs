using System.Collections.Generic;
using Unity.Scripting.LifecycleManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HyperRTS.Presentation.Rendering
{
    /// <summary>
    /// Shadow map size and render scale, set on the active URP asset in memory. The asset is never marked dirty, and
    /// the authored values come back on quit (leaving play mode in the editor), so the asset on disk keeps its own.
    /// </summary>
    public static class PipelineAssetQuality
    {
        [NoAutoStaticsCleanup]
        private static readonly Dictionary<UniversalRenderPipelineAsset, (int Shadows, float Scale)> Authored = new();

        /// <summary>Sets the main light shadow map size (a power of two, 256 to 8192) on the active pipeline asset.</summary>
        internal static void SetShadowResolution(int resolution) => Active().mainLightShadowmapResolution = resolution;

        /// <summary>Sets the share of the screen resolution the cameras render at; the asset's upscaler fills the rest.</summary>
        internal static void SetRenderScale(float scale) => Active().renderScale = scale;

        private static UniversalRenderPipelineAsset Active()
        {
            if (GraphicsSettings.currentRenderPipeline is not UniversalRenderPipelineAsset asset)
            {
                throw new System.InvalidOperationException("HyperRTS: the active render pipeline isn't URP.");
            }

            if (Authored.Count == 0)
            {
                Application.quitting += Restore;
            }

            Authored.TryAdd(asset, (asset.mainLightShadowmapResolution, asset.renderScale));
            return asset;
        }

        private static void Restore()
        {
            Application.quitting -= Restore;
            foreach (var (asset, authored) in Authored)
            {
                if (asset != null)
                {
                    asset.mainLightShadowmapResolution = authored.Shadows;
                    asset.renderScale = authored.Scale;
                }
            }

            Authored.Clear();
        }
    }
}
