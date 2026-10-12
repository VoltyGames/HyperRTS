using System.Collections.Generic;
using System.Linq;
using HyperRTS.Editor.Maps.Geometry;
using HyperRTS.Editor.Maps.Symmetry;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Planning
{
    /// <summary>The roads an access track can run to, and whether a straight run to one crosses water or a ravine.</summary>
    public sealed class MapAccessRoutes
    {
        private const float SampleStep = 1f;
        private const float ClearStep = 1.5f;
        private const float WaterMargin = 1f;
        private const float RavineMargin = 2f;

        private readonly List<(MapRoad Road, List<Vector2> Samples)> _roads;
        private readonly MapShapes _shapes;

        /// <summary>Every authored road, sampled where it lies in the authored sector.</summary>
        public MapAccessRoutes(MapShapes shapes, MapSymmetry symmetry)
        {
            _shapes = shapes;
            _roads = shapes.Roads.Where(r => !r.Generated)
                .Select(r => (Road: r, Samples: MapCurves.Resample(r.Line.Points, SampleStep).Where(symmetry.InSector).ToList()))
                .Where(r => r.Samples.Count > 0)
                .ToList();
        }

        /// <summary>The nearest sector point of each road, nearest road first.</summary>
        public List<(MapRoad Road, Vector2 Point, float Distance)> Nearest(Vector2 from) =>
            _roads.Select(r => Closest(r.Road, r.Samples, from)).OrderBy(c => c.Distance).ToList();

        /// <summary>True when a straight run keeps off the water and out of ravines.</summary>
        public bool Clear(Vector2 from, Vector2 to)
        {
            var count = Mathf.Max(1, Mathf.CeilToInt((to - from).magnitude / ClearStep));
            for (var i = 0; i <= count; i++)
            {
                var point = Vector2.Lerp(from, to, (float)i / count);
                if (_shapes.RiverEdgeDistance(point) < WaterMargin || _shapes.InRavine(point, RavineMargin))
                {
                    return false;
                }
            }

            return true;
        }

        private static (MapRoad Road, Vector2 Point, float Distance) Closest(MapRoad road, List<Vector2> samples,
            Vector2 from)
        {
            var best = samples[0];
            foreach (var sample in samples)
            {
                if ((sample - from).sqrMagnitude < (best - from).sqrMagnitude)
                {
                    best = sample;
                }
            }

            return (road, best, (best - from).magnitude);
        }
    }
}
