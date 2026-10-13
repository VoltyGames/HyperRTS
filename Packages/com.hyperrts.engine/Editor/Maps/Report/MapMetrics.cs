using System;
using System.Collections.Generic;

namespace HyperRTS.Editor.Maps.Report
{
    /// <summary>What a built map measured, as the report's JSON stores it.</summary>
    [Serializable]
    public sealed class MapMetrics
    {
        /// <summary>Fastest and slowest drive (seconds) from a base to its nearest enemy base.</summary>
        public float minRush;
        public float maxRush;

        /// <summary>Separate attack paths between slots 1 and 2.</summary>
        public int paths;

        /// <summary>Most cells a base-to-base path search expanded.</summary>
        public int maxExpansions;

        /// <summary>Renewable fields (clusters of regrowing stacks, see <see cref="MapFieldClusters"/>) per player.</summary>
        public float renewableFieldsPerPlayer;

        public int trees;
        public int placements;
        public int decals;
        public List<MapSpawnMetrics> spawns = new();
    }
}
