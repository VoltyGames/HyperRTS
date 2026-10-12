using System.Collections.Generic;
using System.Linq;
using HyperRTS.Editor.Maps.Geometry;
using HyperRTS.Editor.Maps.Symmetry;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Refine
{
    /// <summary>
    /// Joins a road end that stops short of another sector road onto it: the end snaps to the nearest point of that
    /// road and the last stretch is re-laid as a curve that meets it at 60° or more (a road reaching the end of
    /// another simply continues it). Roads join in descending rank, so a lower-ranked road meets a finished
    /// higher-ranked one.
    /// </summary>
    public static class MapRoadJoiner
    {
        private const float Reach = 12f;
        private const float MinApproach = 60f;
        private const float ForcedApproach = 62f;
        private const float TailBase = 20f;
        private const float SeamTolerance = 1f;
        private const float EndTouch = 1.5f;
        // An end this close that already meets at a steep angle just moves onto the road: re-laying a curve over a
        // short spur would kink it.
        private const float Snapped = 3f;
        private static readonly float[] Handles = { 0.4f, 0.2f };

        public static void Join(List<MapRefinedRoad> roads, MapSymmetry symmetry, Rect playable)
        {
            foreach (var road in roads.OrderByDescending(r => r.Class.rank))
            {
                JoinEnd(road, roads, symmetry, playable, true);
                JoinEnd(road, roads, symmetry, playable, false);
            }
        }

        private static void JoinEnd(MapRefinedRoad road, List<MapRefinedRoad> roads, MapSymmetry symmetry, Rect playable,
            bool atStart)
        {
            var line = atStart ? Reversed(road.Points) : road.Points;
            var end = line[^1];
            var pinned = symmetry.SeamDistance(end) < SeamTolerance || !playable.Contains(end)
                || MapGeometry.OnEdge(end, playable);
            if (pinned)
            {
                return;
            }

            var finish = (end - line[^2]).normalized;
            var target = Nearest(road, roads, symmetry, end, finish);
            if (target == null)
            {
                return;
            }

            var joined = Connect(line, target, symmetry);
            road.Points = atStart ? Reversed(joined) : joined;
        }

        /// <summary>
        /// The closest point of another road within reach of the end and ahead of it, or null. A road behind the end
        /// (a spur leaving it) isn't joined: that would fold the road back onto itself.
        /// </summary>
        private static Target Nearest(MapRefinedRoad road, List<MapRefinedRoad> roads, MapSymmetry symmetry, Vector2 end,
            Vector2 finish)
        {
            Target best = null;
            var reach = Reach + road.Class.width;
            foreach (var other in roads)
            {
                if (other == road)
                {
                    continue;
                }

                var distance = MapGeometry.DistanceToPolyline(other.Points, end, out var along);
                if (distance > reach || (best != null && distance >= best.Distance))
                {
                    continue;
                }

                var length = MapGeometry.Length(other.Points);
                var point = MapGeometry.PointAlong(other.Points, along, out var tangent);
                var continues = along <= EndTouch || along >= length - EndTouch;
                if (continues)
                {
                    point = along <= EndTouch ? other.Points[0] : other.Points[^1];
                }

                if (!symmetry.InSector(point))
                {
                    continue;
                }

                var behind = Vector2.Dot(point - end, finish) <= 0f;
                if (behind && distance > Snapped)
                {
                    continue;
                }

                best = new Target(point, tangent, distance, continues);
            }

            return best;
        }

        /// <summary>The line re-laid so it ends on the target, bending only as far back as it needs.</summary>
        private static List<Vector2> Connect(List<Vector2> line, Target target, MapSymmetry symmetry)
        {
            var end = line[^1];
            var gap = (target.Point - end).magnitude;
            var finish = (end - line[^2]).normalized;
            if (gap <= Snapped && (target.Continues || Angle(finish, target.Tangent) >= MinApproach))
            {
                var snapped = new List<Vector2>(line);
                snapped[^1] = target.Point;
                return snapped;
            }

            var length = MapGeometry.Length(line);
            var tail = Mathf.Min(length * 0.5f, TailBase + 2f * gap);
            var from = MapGeometry.PointAlong(line, length - tail, out var heading);
            var natural = (target.Point - from).normalized;
            var approach = target.Continues || Angle(natural, target.Tangent) >= MinApproach
                ? natural
                : Forced(natural, target.Tangent);
            var stem = Prefix(line, length - tail);
            foreach (var handle in Handles)
            {
                var joined = new List<Vector2>(stem);
                joined.AddRange(Bezier(from, heading, target.Point, approach, handle));
                if (joined.TrueForAll(symmetry.InSector))
                {
                    return MapCurves.Resample(joined, MapRoadRefiner.Step);
                }
            }

            // Straight between two sector points, so it stays in the (convex) sector.
            stem.AddRange(Bezier(from, heading, target.Point, approach, 0f));
            return MapCurves.Resample(stem, MapRoadRefiner.Step);
        }

        /// <summary>The direction at <see cref="ForcedApproach"/>° to the target, on the side the road comes from.</summary>
        private static Vector2 Forced(Vector2 natural, Vector2 tangent)
        {
            var along = Vector2.Dot(natural, tangent) >= 0f ? tangent : -tangent;
            var side = along.x * natural.y - along.y * natural.x >= 0f ? 1f : -1f;
            return MapGeometry.Rotate(along, side * ForcedApproach);
        }

        /// <summary>Acute angle between two directions treated as lines (0 to 90°).</summary>
        private static float Angle(Vector2 a, Vector2 b) =>
            Mathf.Acos(Mathf.Clamp01(Mathf.Abs(Vector2.Dot(a, b)))) * Mathf.Rad2Deg;

        /// <summary>The points before <paramref name="distance"/> metres along the line, then the point at it.</summary>
        private static List<Vector2> Prefix(List<Vector2> line, float distance)
        {
            var prefix = new List<Vector2>();
            var walked = 0f;
            for (var i = 0; i < line.Count; i++)
            {
                walked += i > 0 ? (line[i] - line[i - 1]).magnitude : 0f;
                if (walked >= distance - 0.01f)
                {
                    break;
                }

                prefix.Add(line[i]);
            }

            prefix.Add(MapGeometry.PointAlong(line, distance, out _));
            return prefix;
        }

        /// <summary>Cubic curve leaving <paramref name="from"/> along <paramref name="heading"/> and arriving along <paramref name="approach"/>.</summary>
        private static IEnumerable<Vector2> Bezier(Vector2 from, Vector2 heading, Vector2 to, Vector2 approach, float handle)
        {
            var span = (to - from).magnitude;
            var a = from + heading * (span * handle);
            var b = to - approach * (span * handle);
            var count = Mathf.Max(4, Mathf.CeilToInt(span * 2f));
            for (var k = 1; k <= count; k++)
            {
                var t = (float)k / count;
                var s = 1f - t;
                yield return s * s * s * from + 3f * s * s * t * a + 3f * s * t * t * b + t * t * t * to;
            }
        }

        private static List<Vector2> Reversed(List<Vector2> line)
        {
            var reversed = new List<Vector2>(line);
            reversed.Reverse();
            return reversed;
        }

        private sealed class Target
        {
            public Target(Vector2 point, Vector2 tangent, float distance, bool continues)
            {
                Point = point;
                Tangent = tangent;
                Distance = distance;
                Continues = continues;
            }

            public Vector2 Point { get; }

            public Vector2 Tangent { get; }

            public float Distance { get; }

            /// <summary>The point is the end of the other road: this road carries on from it.</summary>
            public bool Continues { get; }
        }
    }
}
