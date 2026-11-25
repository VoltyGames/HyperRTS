using System;
using HyperRTS.Presentation.Rendering.LightTransport;
using UnityEditor;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HyperRTS.Editor.Validation.Project
{
    /// <summary>
    /// The project settings static light transport needs: the <see cref="LightTransportLayer"/> name, and a Surface
    /// Cache volume limited to that layer. Checks for the validator and their quick fixes.
    /// </summary>
    public static class LightTransportSettings
    {
        /// <summary>The name of the light transport layer in Tags and Layers; empty when unnamed.</summary>
        public static string LayerName() => RenderingLayerNames.Name(LightTransportLayer.Index);

        public static void NameLayer() => RenderingLayerNames.SetName(LightTransportLayer.Index, LightTransportLayer.Name);

        public static bool Restricted(VolumeProfile profile)
        {
            if (!profile.TryGet(out SurfaceCacheGIVolumeOverride surfaceCache))
            {
                return false;
            }

            var mask = surfaceCache.lightTransportRenderingLayerMask;
            return mask.overrideState && (uint)mask.value == LightTransportLayer.Mask;
        }

        public static void Restrict(VolumeProfile profile)
        {
            if (!profile.TryGet(out SurfaceCacheGIVolumeOverride surfaceCache))
            {
                throw new InvalidOperationException($"HyperRTS: the volume profile '{profile.name}' has no Surface Cache GI override.");
            }

            surfaceCache.lightTransportRenderingLayerMask.overrideState = true;
            surfaceCache.lightTransportRenderingLayerMask.value = LightTransportLayer.Mask;
            EditorUtility.SetDirty(profile);
        }
    }
}
