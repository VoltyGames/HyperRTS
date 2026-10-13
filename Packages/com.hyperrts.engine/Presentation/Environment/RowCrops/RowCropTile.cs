using System;

namespace HyperRTS.Presentation.Environment.RowCrops
{
    /// <summary>A <see cref="RowCropFields.TileSize"/> square of the map where crops grow, and the fields that reach into it.</summary>
    [Serializable]
    public struct RowCropTile
    {
        /// <summary>Tile column and row from the field area's minimum corner.</summary>
        public int x;
        public int z;

        /// <summary>The tile's fields: this many entries of <see cref="RowCropFields.tileFields"/> from the first.</summary>
        public int firstField;
        public int fieldCount;
    }
}
