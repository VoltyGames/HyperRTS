using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HyperRTS.Editor.Maps.Views;

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

        /// <summary>Renewable fields (clusters of regrowing stacks, see <see cref="MapResourceFields"/>) per player.</summary>
        public float renewableFieldsPerPlayer;

        public int trees;
        public int placements;
        public int decals;
        public int wetDecals;
        public List<MapSpawnMetrics> spawns = new();

        /// <summary>The ground views' mean <see cref="MapViewContrast.LuminanceSpread"/>; the overview is left out.</summary>
        public float viewLuminanceSpread;

        /// <summary>The ground views' mean <see cref="MapViewContrast.LocalContrast"/>; the overview is left out.</summary>
        public float viewLocalContrast;

        /// <summary>Measures the light and shade of the views just captured.</summary>
        public void MeasureViews(IEnumerable<string> files, string overviewName)
        {
            var views = files.Where(f => Path.GetFileNameWithoutExtension(f) != overviewName)
                .Select(MapViewContrast.Load).ToList();
            if (views.Count == 0)
            {
                return;
            }

            viewLuminanceSpread = views.Average(v => v.LuminanceSpread);
            viewLocalContrast = views.Average(v => v.LocalContrast);
        }
    }
}
