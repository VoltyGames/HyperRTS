using System.Collections.Generic;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Geometry
{
    /// <summary>
    /// Rounds the corners of a polyline into circular arcs. An arc never takes more than half of either segment it
    /// leaves, so a corner between short segments gets a tighter arc than asked for rather than overlapping its
    /// neighbours; the end points stay where they are.
    /// </summary>
    public static class MapFillet
    {
        private const float StraightDegrees = 1f;
        private const float HairpinDegrees = 2f;
        private const float MinSeparation = 0.01f;

        /// <summary>Arcs are drawn with fine chords, so resampling them keeps their radius true.</summary>
        private const float ArcStep = 0.5f;

        /// <summary>The polyline with every corner rounded to <paramref name="radius"/>, resampled every <paramref name="step"/> metres.</summary>
        public static List<Vector2> Round(IReadOnlyList<Vector2> points, float radius, float step)
        {
            var distinct = Distinct(points);
            if (distinct.Count < 3 || radius <= 0f)
            {
                return MapCurves.Resample(distinct, step);
            }

            var path = new List<Vector2> { distinct[0] };
            for (var i = 1; i + 1 < distinct.Count; i++)
            {
                AddCorner(path, distinct[i - 1], distinct[i], distinct[i + 1], radius);
            }

            path.Add(distinct[^1]);
            return MapCurves.Resample(path, step);
        }

        /// <summary>Radius of the circle through three points, or infinity when they are in line.</summary>
        public static float Circumradius(Vector2 a, Vector2 b, Vector2 c)
        {
            var area2 = Mathf.Abs((b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x));
            if (area2 < 1e-6f)
            {
                return float.PositiveInfinity;
            }

            return (b - a).magnitude * (c - b).magnitude * (c - a).magnitude / (2f * area2);
        }

        private static void AddCorner(List<Vector2> path, Vector2 a, Vector2 b, Vector2 c, float radius)
        {
            var toA = a - b;
            var toC = c - b;
            var u = toA / toA.magnitude;
            var v = toC / toC.magnitude;
            var interior = Mathf.Acos(Mathf.Clamp(Vector2.Dot(u, v), -1f, 1f));
            var deflection = Mathf.PI - interior;
            if (deflection < StraightDegrees * Mathf.Deg2Rad || interior < HairpinDegrees * Mathf.Deg2Rad)
            {
                path.Add(b);
                return;
            }

            var half = Mathf.Tan(interior * 0.5f);
            var reach = Mathf.Min(radius / half, Mathf.Min(toA.magnitude, toC.magnitude) * 0.5f);
            var arc = reach * half;
            var center = b + (u + v).normalized * (arc / Mathf.Sin(interior * 0.5f));
            var from = b + u * reach;
            var to = b + v * reach;
            var turn = Mathf.Sign(-u.x * v.y + u.y * v.x);
            var start = Mathf.Atan2(from.y - center.y, from.x - center.x);
            var count = Mathf.Max(2, Mathf.CeilToInt(arc * deflection / ArcStep));
            path.Add(from);
            for (var k = 1; k < count; k++)
            {
                var angle = start + turn * deflection * k / count;
                path.Add(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * arc);
            }

            path.Add(to);
        }

        private static List<Vector2> Distinct(IReadOnlyList<Vector2> points)
        {
            var distinct = new List<Vector2>(points.Count);
            foreach (var point in points)
            {
                if (distinct.Count == 0 || (point - distinct[^1]).magnitude > MinSeparation)
                {
                    distinct.Add(point);
                }
            }

            return distinct;
        }
    }
}
