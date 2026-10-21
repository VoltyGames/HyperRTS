using System;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace HyperRTS.Presentation.Overlays
{
    /// <summary>
    /// The overlay material and its runtime clones: "on top" ones skip the depth test so the local player's selection
    /// shows through buildings, and the "front" one draws after them so dial and pip fills sit over their backs.
    /// </summary>
    public sealed class OverlayMaterials : IDisposable
    {
        private static readonly int ZTestId = Shader.PropertyToID("_ZTest");

        public readonly Material DepthTested;
        public readonly Material OnTop;
        public readonly Material OnTopFront;

        public OverlayMaterials(Material material)
        {
            if (!material.HasProperty(ZTestId))
            {
                throw new InvalidOperationException(
                    $"Overlay material '{material.name}' has no _ZTest property; use the HyperRTS/Overlay shader.");
            }

            DepthTested = material;
            OnTop = Clone(material, 2, CompareFunction.Always);
            OnTopFront = Clone(material, 3, CompareFunction.Always);
        }

        public void Dispose()
        {
            Object.Destroy(OnTop);
            Object.Destroy(OnTopFront);
        }

        private static Material Clone(Material material, int queueOffset, CompareFunction zTest)
        {
            var clone = new Material(material)
            {
                renderQueue = material.renderQueue + queueOffset,
                hideFlags = HideFlags.DontSave,
            };
            clone.SetFloat(ZTestId, (float)zTest);
            return clone;
        }
    }
}
