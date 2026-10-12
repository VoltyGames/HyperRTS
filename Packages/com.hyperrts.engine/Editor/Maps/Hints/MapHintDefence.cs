using System.Collections.Generic;
using System.Linq;
using HyperRTS.Editor.Maps.Nav;
using Unity.Mathematics;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Hints
{
    /// <summary>
    /// Where to hold the ground in front of a base: for each lane leading to it, the spot nearest the base where the
    /// lane passes a crossing or narrow ground, else where the lane leaves the base. Nearby spots merge.
    /// </summary>
    public static class MapHintDefence
    {
        /// <summary>Most defence points one base gets.</summary>
        public const int MaxPoints = 6;

        /// <summary>How far past the base's edge a lane is searched for a choke (metres).</summary>
        public const float Depth = 80f;

        /// <summary>Spots closer than this are one spot (metres).</summary>
        public const float MergeDistance = 24f;

        /// <summary>Directions (spread over half a turn) a spot's open width is measured along.</summary>
        private const int ChordDirections = 8;

        public static List<Vector2> Find(MapHintInput input, MapHintBase target, IEnumerable<MapHintLane> lanes)
        {
            var points = new List<Vector2>();
            foreach (var lane in lanes)
            {
                var outward = lane.CellsLeaving(target);
                var spot = outward == null ? null : Choke(input, target, outward);
                if (spot is { } point && points.TrueForAll(p => Vector2.Distance(p, point) >= MergeDistance))
                {
                    points.Add(point);
                }
            }

            return points.OrderBy(p => Vector2.Distance(p, target.Position)).ThenBy(p => p.x).ThenBy(p => p.y)
                .Take(MaxPoints).ToList();
        }

        /// <summary>First crossing or narrow spot walking out of the base along the lane, else where the lane leaves it.</summary>
        private static Vector2? Choke(MapHintInput input, MapHintBase target, List<int2> outward)
        {
            Vector2? entry = null;
            for (var i = 0; i < outward.Count; i++)
            {
                var point = Point(input.Nav, outward[i]);
                var away = Vector2.Distance(point, target.Position);
                if (away < input.BaseRadius)
                {
                    continue;
                }

                entry ??= point;
                if (away > input.BaseRadius + Depth)
                {
                    break;
                }

                if (IsChoke(input, point))
                {
                    return point;
                }
            }

            return entry;
        }

        private static bool IsChoke(MapHintInput input, Vector2 point)
        {
            foreach (var (position, radius) in input.Crossings)
            {
                if (Vector2.Distance(position, point) <= radius)
                {
                    return true;
                }
            }

            return IsNarrow(input, point);
        }

        /// <summary>
        /// Whether some straight line through the point has less than a vehicle column's width of open ground along
        /// it. Any direction counts: a lane need not meet a gap square on.
        /// </summary>
        private static bool IsNarrow(MapHintInput input, Vector2 point)
        {
            for (var k = 0; k < ChordDirections; k++)
            {
                var angle = Mathf.PI * k / ChordDirections;
                var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                var width = OpenReach(input.Nav, point, direction, input.CorridorWidth) +
                            OpenReach(input.Nav, point, -direction, input.CorridorWidth);
                if (width < input.CorridorWidth)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Metres of walkable ground from a point along a direction, up to <paramref name="limit"/>.</summary>
        private static float OpenReach(MapNavGrid nav, Vector2 point, Vector2 direction, float limit)
        {
            var step = nav.Grid.CellSize;
            var reach = 0f;
            while (reach < limit && nav.IsWalkable(nav.Cell(point + direction * (reach + step))))
            {
                reach += step;
            }

            return reach;
        }

        private static Vector2 Point(MapNavGrid nav, int2 cell)
        {
            var center = nav.Center(cell);
            return new Vector2(center.x, center.z);
        }
    }
}
