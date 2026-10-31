using System.Collections.Generic;
using HyperRTS.Editor.Maps.Geometry;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Designer
{
    /// <summary>
    /// Vertex counts and segment picking for lines and areas: a line keeps at least two points, an area three, and a
    /// vertex added on segment <c>i</c> goes in at index <c>i + 1</c>.
    /// </summary>
    public static class MapVertexRules
    {
        private const int MinLinePoints = 2;
        private const int MinAreaPoints = 3;

        /// <summary>The fewest points a feature of this geometry keeps; 1 for points and circles.</summary>
        public static int MinPoints(MapFeatureGeometry geometry) => geometry switch
        {
            MapFeatureGeometry.Polyline => MinLinePoints,
            MapFeatureGeometry.Polygon => MinAreaPoints,
            _ => 1,
        };

        /// <summary>Whether a vertex can go from a line or area of <paramref name="count"/> points.</summary>
        public static bool CanRemove(MapFeatureGeometry geometry, int count)
        {
            var min = MinPoints(geometry);
            return min > 1 && count > min;
        }

        /// <summary>
        /// The segment nearest <paramref name="p"/> within <paramref name="maxDistance"/>, or -1. Segment <c>i</c> runs
        /// from point <c>i</c> to <c>i + 1</c>; a closed ring's last segment runs back to point 0.
        /// </summary>
        public static int SegmentAt(IReadOnlyList<Vector2> points, bool closed, Vector2 p, float maxDistance)
        {
            var best = -1;
            var bestDistance = maxDistance;
            var segments = closed ? points.Count : points.Count - 1;
            for (var i = 0; i < segments; i++)
            {
                var distance = MapGeometry.DistanceToSegment(points[i], points[(i + 1) % points.Count], p);
                if (distance <= bestDistance)
                {
                    best = i;
                    bestDistance = distance;
                }
            }

            return best;
        }
    }
}
