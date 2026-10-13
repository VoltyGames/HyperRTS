using System.Linq;
using UnityEngine;

namespace HyperRTS.Presentation.Rendering
{
    /// <summary>The rendering layer unit renderers carry so UnitSilhouetteFeature can draw them through cover.</summary>
    public static class UnitRenderingLayer
    {
        public const int Index = 8;
        public const string Name = "Units";

        public const uint Mask = 1u << Index;

        /// <summary>Renderers under <paramref name="root"/> that lack the units layer.</summary>
        public static Renderer[] Missing(GameObject root) =>
            root.GetComponentsInChildren<Renderer>(true)
                .Where(renderer => (renderer.renderingLayerMask & Mask) == 0)
                .ToArray();

        public static void Assign(GameObject root)
        {
            foreach (var renderer in Missing(root))
            {
                renderer.renderingLayerMask |= Mask;
            }
        }
    }
}
