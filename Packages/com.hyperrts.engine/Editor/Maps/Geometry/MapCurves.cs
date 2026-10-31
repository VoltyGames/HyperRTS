using System.Collections.Generic;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Geometry
{
    /// <summary>Curves through authored polylines: Catmull-Rom splines sampled evenly, and moving-average smoothing.</summary>
    public static class MapCurves
    {
        /// <summary>A uniform Catmull-Rom curve through every point, sampled every <paramref name="step"/> metres.</summary>
        public static List<Vector2> Spline(IReadOnlyList<Vector2> points, float step)
        {
            if (points.Count < 3)
            {
                return Resample(points, step);
            }

            var dense = new List<Vector2> { points[0] };
            for (var i = 0; i + 1 < points.Count; i++)
            {
                var p0 = i > 0 ? points[i - 1] : 2f * points[0] - points[1];
                var p3 = i + 2 < points.Count ? points[i + 2] : 2f * points[^1] - points[^2];
                var p1 = points[i];
                var p2 = points[i + 1];
                var pieces = Mathf.Max(2, Mathf.CeilToInt((p2 - p1).magnitude / (step * 0.5f)));
                for (var k = 1; k <= pieces; k++)
                {
                    dense.Add(CatmullRom(p0, p1, p2, p3, (float)k / pieces));
                }
            }

            return Resample(dense, step);
        }

        /// <summary>Points every <paramref name="step"/> metres along a polyline, both ends kept.</summary>
        public static List<Vector2> Resample(IReadOnlyList<Vector2> line, float step)
        {
            var length = MapGeometry.Length(line);
            var count = Mathf.Max(1, Mathf.RoundToInt(length / step));
            var points = new List<Vector2>(count + 1);
            var path = new MapLineWalker(line);
            for (var i = 0; i <= count; i++)
            {
                points.Add(path.PointAt(length * i / count, out _));
            }

            return points;
        }

        /// <summary>Moving average over ±<paramref name="reach"/> points; the ends stay where they are.</summary>
        public static List<Vector2> Smooth(IReadOnlyList<Vector2> line, int reach)
        {
            var smoothed = new List<Vector2>(line.Count);
            for (var i = 0; i < line.Count; i++)
            {
                var span = Mathf.Min(reach, Mathf.Min(i, line.Count - 1 - i));
                var sum = Vector2.zero;
                for (var j = i - span; j <= i + span; j++)
                {
                    sum += line[j];
                }

                smoothed.Add(sum / (span * 2 + 1));
            }

            return smoothed;
        }

        private static Vector2 CatmullRom(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
        {
            var t2 = t * t;
            var t3 = t2 * t;
            return 0.5f * (2f * p1 + (p2 - p0) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2
                + (3f * p1 - p0 - 3f * p2 + p3) * t3);
        }
    }
}
