using System.Collections.Generic;
using HyperRTS.Editor.Maps.Geometry;
using HyperRTS.Presentation.Environment.Vegetation.RowCrops;
using HyperRTS.Presentation.Environment.Vegetation.Scatter;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Ground
{
    /// <summary>Where a map's row crops grow, before <see cref="MapCropWriter"/> saves it.</summary>
    public sealed class MapCropBake
    {
        public MapCropBake(MapGrid grid)
        {
            Ids = new MapValues<int>(grid);
        }

        /// <summary>World XZ rectangle the cells cover, one metre each.</summary>
        public Rect Area => Ids.Grid.Bounds;

        public int Width => Ids.Grid.Width;

        public int Height => Ids.Grid.Height;

        /// <summary>Field index plus one per cell. 0 means no crop.</summary>
        public MapValues<int> Ids { get; }

        public List<RowCrop> Crops { get; } = new();

        /// <summary>Share of sight each of <see cref="Crops"/> takes per metre crossed.</summary>
        public List<float> SightDensities { get; } = new();

        public List<RowCropField> Fields { get; } = new();

        public List<ScatterTile> Tiles { get; } = new();

        public List<int> TileFields { get; } = new();

        /// <summary>Plants the fields grow at full density.</summary>
        public long Plants { get; set; }

        public bool IsEmpty => Fields.Count == 0;

        public int Id(int x, int z) => Ids[x, z];
    }
}
