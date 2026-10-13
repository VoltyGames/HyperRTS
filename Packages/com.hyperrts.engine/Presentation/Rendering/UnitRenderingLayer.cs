using System.Linq;
using UnityEngine;

namespace HyperRTS.Presentation.Rendering
{
    /// <summary>The rendering layer unit renderers carry so UnitSilhouetteFeature can draw them through cover.</summary>
    public static class UnitRenderingLayer
    {
        public const int Index = 8;
        public const string Name = "Units";

        public static uint Mask(int index) => 1u << index;

        /// <summary>Renderers under <paramref name="root"/> that lack rendering layer <paramref name="index"/>.</summary>
        public static Renderer[] Missing(GameObject root, int index) =>
            root.GetComponentsInChildren<Renderer>(true)
                .Where(renderer => (renderer.renderingLayerMask & Mask(index)) == 0)
                .ToArray();

        public static void Assign(GameObject root, int index)
        {
            foreach (var renderer in Missing(root, index))
            {
                renderer.renderingLayerMask |= Mask(index);
            }
        }
    }
}
