using HyperRTS.Editor.Maps.Spec;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Geometry
{
    /// <summary>
    /// A ravine in map space: its line and spec, and where its wall stands (the distance from the line plus noise), so
    /// the height shaper's carving and the land planner's claims follow the same wandering edge.
    /// </summary>
    public sealed class MapRavine
    {
        private const float EdgeWander = 1.5f;
        private const float EdgeWanderWavelength = 15f;
        private readonly MapNoise _edgeWander;

        public MapRavine(MapLine line, MapRavineSpec spec, MapNoise edgeWander)
        {
            Line = line;
            Spec = spec;
            _edgeWander = edgeWander;
        }

        public MapLine Line { get; }

        public MapRavineSpec Spec { get; }

        /// <summary>Wandering distance to the ravine line, infinity outside its reach.</summary>
        public float WallDistance(Vector2 point) => WallDistance(point, out _);

        /// <summary>Wandering distance to the ravine line and the closest point's distance along it.</summary>
        public float WallDistance(Vector2 point, out float along)
        {
            var distance = Line.Distance(point, out along);
            return float.IsPositiveInfinity(distance) ? distance : distance + _edgeWander.Sample(point, EdgeWanderWavelength) * EdgeWander;
        }
    }
}
