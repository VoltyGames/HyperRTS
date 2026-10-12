using System.Collections.Generic;
using HyperRTS.Editor.Maps.Geometry;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Meshes
{
    /// <summary>
    /// Which road ends fade out in the mesh: free ends, and ends where a road meets one of its own rank or higher
    /// (it fades out under it). An end that carries on into another road's end (a seam, a split road) or runs onto a
    /// lower-ranked road stays solid.
    /// </summary>
    public static class MapRoadFades
    {
        private const float Continues = 1.5f;

        /// <summary>Whether the road's start and end fade.</summary>
        public static (bool Start, bool End) For(MapRoad road, IReadOnlyList<MapRoad> roads)
        {
            var points = road.Line.Points;
            return (Fades(road, points[0], roads), Fades(road, points[^1], roads));
        }

        private static bool Fades(MapRoad road, Vector2 end, IReadOnlyList<MapRoad> roads)
        {
            var touching = false;
            var underHigher = false;
            foreach (var other in roads)
            {
                if (other == road)
                {
                    continue;
                }

                var distance = other.Line.Distance(end, out var along);
                if (distance > other.Width * 0.5f)
                {
                    continue;
                }

                var atEnd = along <= Continues || along >= other.Line.Length - Continues;
                if (atEnd && distance <= Continues)
                {
                    return false;
                }

                touching = true;
                underHigher |= road.Class.rank <= other.Class.rank;
            }

            return !touching || underHigher;
        }
    }
}
