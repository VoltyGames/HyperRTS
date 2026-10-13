using System.Collections.Generic;
using HyperRTS.Presentation.Environment.RowCrops;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Ground
{
    /// <summary>
    /// Where a map's row crops grow, before it is written to assets: a field id per land cell (0 for none), the
    /// fields' lattices, the crops and the tiles that hold any crop. <see cref="MapCropWriter"/> saves it as a
    /// <see cref="RowCropFields"/>.
    /// </summary>
    public sealed class MapCropBake
    {
        public MapCropBake(Rect area, int width, int height)
        {
            Area = area;
            Width = width;
            Height = height;
            Ids = new int[width * height];
        }

        /// <summary>World XZ rectangle the cells cover, one metre each.</summary>
        public Rect Area { get; }

        public int Width { get; }

        public int Height { get; }

        /// <summary>Field id per cell, row by row from the minimum corner: field index plus one, 0 for no crop.</summary>
        public int[] Ids { get; }

        public List<RowCrop> Crops { get; } = new();

        public List<RowCropField> Fields { get; } = new();

        public List<RowCropTile> Tiles { get; } = new();

        public List<int> TileFields { get; } = new();

        /// <summary>Plants the fields grow at full density, from their sown area and spacing.</summary>
        public long Plants { get; set; }

        public bool IsEmpty => Fields.Count == 0;

        public int Id(int x, int z) => Ids[z * Width + x];
    }
}
