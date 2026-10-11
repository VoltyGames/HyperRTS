using System.Collections.Generic;
using HyperRTS.Editor.Maps.Data;
using HyperRTS.Editor.Maps.Geometry;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Planning
{
    /// <summary>
    /// Lays a track a planner chose in the authored sector (along field ends, up to a prop's pad): the road in every
    /// copy, and its land. A track on a symmetry axis maps onto itself and is laid once.
    /// </summary>
    public static class MapTrackLayer
    {
        public static void Lay(MapPlanContext context, MapRoadClass track, List<Vector2> sectorLine)
        {
            var laid = new List<List<Vector2>>();
            foreach (var copy in context.Layout.Symmetry.Copies)
            {
                var points = sectorLine.ConvertAll(copy.Apply);
                if (laid.Exists(other => Same(other, points)))
                {
                    continue;
                }

                laid.Add(points);
                var line = new MapLine(points, track.width, track.width * 0.5f + track.vergeWidth + MapShapes.RoadShoulder);
                var road = new MapRoad(line, track, true);
                context.Shapes.AddRoad(road);
                MapRoadPlanner.Claim(context, road);
            }
        }

        /// <summary>Lines with the same ends either way round: on an axis a copy lands on the line itself.</summary>
        private static bool Same(List<Vector2> a, List<Vector2> b)
        {
            if (a.Count != b.Count)
            {
                return false;
            }

            var forward = (a[0] - b[0]).sqrMagnitude < 0.25f && (a[^1] - b[^1]).sqrMagnitude < 0.25f;
            var backward = (a[0] - b[^1]).sqrMagnitude < 0.25f && (a[^1] - b[0]).sqrMagnitude < 0.25f;
            return forward || backward;
        }
    }
}
