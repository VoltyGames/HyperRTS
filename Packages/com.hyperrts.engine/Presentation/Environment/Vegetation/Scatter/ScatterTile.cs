using System;

namespace HyperRTS.Presentation.Environment.Vegetation.Scatter
{
    /// <summary>A <see cref="ScatterTiles.Size"/> square of a field's tile grid, and its run in the field's per-tile list.</summary>
    [Serializable]
    public struct ScatterTile
    {
        /// <summary>Tile column and row from the field area's minimum corner.</summary>
        public int x;
        public int z;

        /// <summary>The tile's run in the field's per-tile list: <see cref="count"/> entries from <see cref="first"/>.</summary>
        public int first;
        public int count;
    }
}
