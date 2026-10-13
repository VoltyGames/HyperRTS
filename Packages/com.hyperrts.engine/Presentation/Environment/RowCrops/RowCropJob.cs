using UnityEngine;

namespace HyperRTS.Presentation.Environment.RowCrops
{
    /// <summary>One visible tile of one field for RowCrops.compute to grow (16 bytes, the layout the kernel declares).</summary>
    public struct RowCropJob
    {
        public const int Stride = 16;

        /// <summary>World XZ of the tile's minimum corner.</summary>
        public Vector2 tileMin;

        /// <summary>Index into the field asset's fields.</summary>
        public int field;

        private int _padding;

        public RowCropJob(Vector2 tileMin, int field)
        {
            this.tileMin = tileMin;
            this.field = field;
            _padding = 0;
        }
    }
}
