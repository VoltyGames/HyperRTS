using UnityEngine;

namespace HyperRTS.Presentation.Environment.RowCrops
{
    /// <summary>One visible tile of one field for RowCrops.compute to grow (28 bytes, the layout the kernel declares).</summary>
    public struct RowCropJob
    {
        public const int Stride = 28;

        /// <summary>World XZ of the tile's minimum corner.</summary>
        public Vector2 tileMin;

        /// <summary>Index into the field asset's fields.</summary>
        public int field;

        /// <summary>The field's lattice spots that can fall in the tile, from <see cref="RowCropLattice.Range"/>.</summary>
        public Vector2Int first;
        public Vector2Int count;

        public RowCropJob(Rect tile, int field, RowCropField data, float rowSpacing, float plantSpacing)
        {
            tileMin = tile.min;
            this.field = field;
            (first, count) = RowCropLattice.Range(tile, data, rowSpacing, plantSpacing);
        }

        /// <summary>Kernel threads the job needs: one per spot.</summary>
        public int Spots => count.x * count.y;
    }
}
