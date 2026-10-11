using System;
using System.Collections.Generic;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Data
{
    /// <summary>A terrain texture layer and the grass that grows wherever it is painted.</summary>
    [Serializable]
    public sealed class MapClimateLayer
    {
        public string key;
        public TerrainLayer terrainLayer;

        [Tooltip("Colour of this ground in the report's overview image.")]
        public Color mapColor = Color.grey;

        public List<MapDetailCoverage> details = new();
    }
}
