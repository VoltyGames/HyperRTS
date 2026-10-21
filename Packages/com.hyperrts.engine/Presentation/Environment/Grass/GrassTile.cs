using System;

namespace HyperRTS.Presentation.Environment.Grass
{
    /// <summary>A <see cref="GrassField.TileSize"/> square of the field's tile grid that grows any plant.</summary>
    [Serializable]
    public struct GrassTile
    {
        public int x;
        public int z;

        /// <summary>The tile's run in <see cref="GrassField.tilePlants"/>.</summary>
        public int firstPlant;
        public int plantCount;
    }
}
