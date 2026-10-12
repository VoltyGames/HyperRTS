using System.Collections.Generic;
using HyperRTS.Editor.Maps.Geometry;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Planning.Settlements
{
    /// <summary>
    /// The roads a settlement lines with plots: every authored road that comes within its radius. Tracks laid by
    /// planners (field and access tracks) lead to fields and pads, not to houses, so they are left out; road classes
    /// carry no "street" flag, so any other class counts.
    /// </summary>
    internal static class MapStreets
    {
        public static List<MapRoad> Near(MapPlanContext context, Vector2 center, float radius)
        {
            var streets = new List<MapRoad>();
            foreach (var road in context.Shapes.Roads)
            {
                if (road.Generated)
                {
                    continue;
                }

                if (MapGeometry.DistanceToPolyline(road.Line.Points, center) <= radius)
                {
                    streets.Add(road);
                }
            }

            return streets;
        }
    }
}
