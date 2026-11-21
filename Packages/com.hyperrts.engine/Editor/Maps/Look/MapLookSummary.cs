using System;
using System.Collections.Generic;
using System.Linq;
using HyperRTS.Editor.Maps.Views;

namespace HyperRTS.Editor.Maps.Look
{
    /// <summary>What a capture run wrote, saved as summary.json beside the images.</summary>
    [Serializable]
    internal sealed class MapLookSummary
    {
        public string map;
        public string quality;

        /// <summary>False when the weathers' Color Lookup was off, for fitting lookups.</summary>
        public bool graded;
        public int width;
        public int height;
        public string failure;
        public List<MapLookShot> shots = new();

        /// <summary>Each weather's mean view numbers; empty until <see cref="MeasureWeathers"/> runs.</summary>
        public List<MapLookWeather> weathers = new();

        /// <summary>Measures the written shots of each weather with <see cref="MapViewContrast"/>.</summary>
        public void MeasureWeathers()
        {
            weathers.Clear();
            foreach (var group in shots.GroupBy(shot => shot.weather))
            {
                var views = group.Select(shot => MapViewContrast.Load(shot.path)).ToList();
                weathers.Add(new MapLookWeather
                {
                    weather = group.Key,
                    viewLuminanceSpread = views.Average(view => view.LuminanceSpread),
                    viewLocalContrast = views.Average(view => view.LocalContrast),
                    viewChroma = views.Average(view => view.Chroma),
                });
            }
        }
    }
}
