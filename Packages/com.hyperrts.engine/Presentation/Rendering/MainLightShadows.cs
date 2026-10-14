using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HyperRTS.Presentation.Rendering
{
    /// <summary>
    /// The main light's shadow map size, set on the active URP asset in memory. The asset is never marked dirty, and
    /// the authored sizes come back on quit (leaving play mode in the editor), so the asset on disk keeps its own.
    /// </summary>
    public static class MainLightShadows
    {
        private static readonly Dictionary<UniversalRenderPipelineAsset, int> Authored = new();

        /// <summary>Sets the shadow map size (a power of two, 256 to 8192) on the active pipeline asset.</summary>
        public static void SetResolution(int resolution)
        {
            if (GraphicsSettings.currentRenderPipeline is not UniversalRenderPipelineAsset asset)
            {
                throw new System.InvalidOperationException("HyperRTS: the active render pipeline isn't URP.");
            }

            if (Authored.Count == 0)
            {
                Application.quitting += Restore;
            }

            Authored.TryAdd(asset, asset.mainLightShadowmapResolution);
            asset.mainLightShadowmapResolution = resolution;
        }

        private static void Restore()
        {
            Application.quitting -= Restore;
            foreach (var (asset, resolution) in Authored)
            {
                if (asset != null)
                {
                    asset.mainLightShadowmapResolution = resolution;
                }
            }

            Authored.Clear();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Application.quitting -= Restore;
            Authored.Clear();
        }
    }
}
