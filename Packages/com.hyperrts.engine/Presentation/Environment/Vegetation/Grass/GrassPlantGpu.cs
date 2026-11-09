using UnityEngine;
using UnityEngine.Rendering;

namespace HyperRTS.Presentation.Environment.Vegetation.Grass
{
    /// <summary>One plant as Grass.compute reads it (Grass.compute includes the generated layout).</summary>
    [GenerateHLSL(needAccessors: false)]
    public struct GrassPlantGpu
    {
        public float spacing;
        public float clumping;
        public Vector2 width;
        public Vector2 height;
        public int variants;

        /// <summary>The plant's first draw slot; its variants and bands follow, as GrassBatch lays them out.</summary>
        public int firstSlot;

        public int seed;

        /// <summary>Metres the plant draws to, or 0 for the quality distance.</summary>
        public float reach;
    }
}
