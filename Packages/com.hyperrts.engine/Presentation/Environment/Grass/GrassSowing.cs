using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

namespace HyperRTS.Presentation.Environment.Grass
{
    /// <summary>One plant as Grass.compute reads it (the layout the kernel declares).</summary>
    public struct GrassSowing
    {
        public static readonly int Stride = UnsafeUtility.SizeOf<GrassSowing>();

        public float spacing;
        public float clumping;
        public Vector2 width;
        public Vector2 height;
        public int variants;

        /// <summary>The plant's first draw slot; its variants and bands follow, as GrassBatch lays them out.</summary>
        public int firstSlot;

        public int seed;
    }
}
