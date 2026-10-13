using HyperRTS.Editor.Maps.Layout;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Geometry
{
    /// <summary>Where a road end leads (road, map edge, base, settlement); shared by the dead-end check and track planners.</summary>
    public static class MapRoadEnds
    {
        /// <summary>An end this close to another road's edge (metres) meets it.</summary>
        public const float Touch = 1f;

        /// <summary>Whether the end reaches the map edge, a settlement, a base or a road other than <paramref name="road"/> (null if unlaid).</summary>
        public static bool LeadsSomewhere(MapLayout layout, MapShapes shapes, MapRoad road, Vector2 end,
            float baseRadius) =>
            LeadsOffRoad(layout, end, baseRadius) || MeetsRoad(shapes, road, end);

        /// <summary>Whether the end reaches the map edge, a settlement or a base, leaving roads out.</summary>
        public static bool LeadsOffRoad(MapLayout layout, Vector2 end, float baseRadius)
        {
            if (!layout.Bounds.Contains(end) || MapGeometry.OnEdge(end, layout.Bounds))
            {
                return true;
            }

            return InSettlement(layout, end) || AtBase(layout, end, baseRadius);
        }

        /// <summary>True when the end touches a road other than <paramref name="road"/>.</summary>
        public static bool MeetsRoad(MapShapes shapes, MapRoad road, Vector2 end)
        {
            foreach (var other in shapes.Roads)
            {
                if (other != road && other.Line.Distance(end) <= other.Width * 0.5f + Touch)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>A road ending at a base leads somewhere: it's the base's way out.</summary>
        private static bool AtBase(MapLayout layout, Vector2 point, float baseRadius)
        {
            foreach (var spawn in layout.Spawns)
            {
                if ((spawn.Position - point).magnitude <= baseRadius + Touch)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool InSettlement(MapLayout layout, Vector2 point)
        {
            foreach (var settlement in layout.Settlements)
            {
                var spec = settlement.Placed;
                if ((spec.center.ToVector2() - point).magnitude <= spec.radius)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
