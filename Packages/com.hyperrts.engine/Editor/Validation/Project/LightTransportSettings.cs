using System;
using HyperRTS.Presentation.Rendering.LightTransport;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HyperRTS.Editor.Validation.Project
{
    /// <summary>
    /// What static light transport needs: the <see cref="LightTransportLayer"/> name, a Surface Cache volume limited
    /// to that layer, and the sun on it. Checks for the validator and their quick fixes.
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

        /// <summary>Whether the light is on the layer: Surface Cache drops a light off its mask, as it drops a mesh.</summary>
        public static bool Bounces(Light light) => ((uint)light.renderingLayerMask & LightTransportLayer.Mask) != 0;

        public static void Bounce(Light light)
        {
            Undo.RecordObject(light, "Bounce Light");
            light.renderingLayerMask |= (int)LightTransportLayer.Mask;
            if (light.TryGetComponent(out UniversalAdditionalLightData data))
            {
                Undo.RecordObject(data, "Bounce Light");
                data.renderingLayers |= LightTransportLayer.Mask;
            }
        }
    }
}
