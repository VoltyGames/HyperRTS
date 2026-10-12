using System.Collections.Generic;
using HyperRTS.Editor.Maps.Spec;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Geometry
{
    /// <summary>Plane geometry over spec polylines and polygons (XZ as Vector2).</summary>
    public static class MapGeometry
    {
        public static List<Vector2> ToVectors(List<MapPoint> points) => points.ConvertAll(p => p.ToVector2());

        public static List<MapPoint> ToPoints(List<Vector2> points) => points.ConvertAll(p => new MapPoint(p.x, p.y));

        /// <summary>Distance from a point to a polyline, with the closest point's distance along the line from its start.</summary>
        public static float DistanceToPolyline(IReadOnlyList<Vector2> line, Vector2 point, out float along)
        {
            var best = float.MaxValue;
            along = 0f;
            var walked = 0f;
            for (var i = 0; i + 1 < line.Count; i++)
            {
                var a = line[i];
                var segment = line[i + 1] - a;
                var length = segment.magnitude;
                var t = length > 0f ? Mathf.Clamp01(Vector2.Dot(point - a, segment) / (length * length)) : 0f;
                var distance = (a + segment * t - point).magnitude;
                if (distance < best)
                {
                    best = distance;
                    along = walked + t * length;
                }

                walked += length;
            }

            return best;
        }

        public static float DistanceToPolyline(IReadOnlyList<Vector2> line, Vector2 point) =>
            DistanceToPolyline(line, point, out _);

        public static float Length(IReadOnlyList<Vector2> line)
        {
            var length = 0f;
            for (var i = 0; i + 1 < line.Count; i++)
            {
                length += (line[i + 1] - line[i]).magnitude;
            }

            return length;
        }

        /// <summary>
        /// The point <paramref name="distance"/> metres along a polyline and the line's direction there. Loops that ask
        /// for many points on one line should build a <see cref="MapPathWalker"/> once instead.
        /// </summary>
        public static Vector2 PointAlong(IReadOnlyList<Vector2> line, float distance, out Vector2 direction) =>
            new MapPathWalker(line).PointAt(distance, out direction);

        public static bool Contains(IReadOnlyList<Vector2> polygon, Vector2 point)
        {
            var inside = false;
            for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
            {
                var a = polygon[i];
                var b = polygon[j];
                if ((a.y > point.y) != (b.y > point.y) && point.x < (b.x - a.x) * (point.y - a.y) / (b.y - a.y) + a.x)
                {
                    inside = !inside;
                }
            }

            return inside;
        }

        /// <summary>Distance to the polygon's edge: positive inside, negative outside.</summary>
        public static float SignedDistance(IReadOnlyList<Vector2> polygon, Vector2 point)
        {
            var edge = float.MaxValue;
            for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
            {
                edge = Mathf.Min(edge, DistanceToSegment(polygon[j], polygon[i], point));
            }

            return Contains(polygon, point) ? edge : -edge;
        }

        public static Rect Bounds(IReadOnlyList<Vector2> points, float margin = 0f)
        {
            var min = points[0];
            var max = points[0];
            foreach (var point in points)
            {
                min = Vector2.Min(min, point);
                max = Vector2.Max(max, point);
            }

            return Rect.MinMaxRect(min.x - margin, min.y - margin, max.x + margin, max.y + margin);
        }

        public static float Area(IReadOnlyList<Vector2> polygon)
        {
            var twice = 0f;
            for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
            {
                twice += polygon[j].x * polygon[i].y - polygon[i].x * polygon[j].y;
            }

            return Mathf.Abs(twice) * 0.5f;
        }

        /// <summary>
        /// The polyline with each end that touches <paramref name="edge"/> run straight on by
        /// <paramref name="distance"/>, so rivers and roads continue past the playable edge.
        /// </summary>
        public static List<Vector2> ExtendAtEdge(List<Vector2> line, Rect edge, float distance)
        {
            var extended = new List<Vector2>(line);
            if (distance <= 0f || line.Count < 2)
            {
                return extended;
            }

            if (OnEdge(line[0], edge))
            {
                extended.Insert(0, line[0] + (line[0] - line[1]).normalized * distance);
            }

            if (OnEdge(line[^1], edge))
            {
                extended.Add(line[^1] + (line[^1] - line[^2]).normalized * distance);
            }

            return extended;
        }

        /// <summary>Within two metres of a rectangle's border.</summary>
        public static bool OnEdge(Vector2 point, Rect edge) =>
            point.x <= edge.xMin + 2f || point.x >= edge.xMax - 2f || point.y <= edge.yMin + 2f
            || point.y >= edge.yMax - 2f;

        /// <summary>Yaw (degrees clockwise from north) of a direction on the map plane.</summary>
        public static float Yaw(Vector2 direction) => Mathf.Atan2(direction.x, direction.y) * Mathf.Rad2Deg;

        public static Vector2 Forward(float yaw)
        {
            var radians = yaw * Mathf.Deg2Rad;
            return new Vector2(Mathf.Sin(radians), Mathf.Cos(radians));
        }

        /// <summary>A direction turned by <paramref name="degrees"/> (counter-clockwise on the XZ plane).</summary>
        public static Vector2 Rotate(Vector2 direction, float degrees)
        {
            var radians = degrees * Mathf.Deg2Rad;
            var cos = Mathf.Cos(radians);
            var sin = Mathf.Sin(radians);
            return new Vector2(direction.x * cos - direction.y * sin, direction.x * sin + direction.y * cos);
        }

        /// <summary>A uniform random point in a rectangle (two draws from <paramref name="random"/>: x, then y).</summary>
        public static Vector2 RandomIn(Rect bounds, System.Random random) =>
            new(Mathf.Lerp(bounds.xMin, bounds.xMax, (float)random.NextDouble()),
                Mathf.Lerp(bounds.yMin, bounds.yMax, (float)random.NextDouble()));

        /// <summary>Smooth 0-to-1 ramp (Hermite), the falloff every terrain feature uses.</summary>
        public static float Smooth(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        private static float DistanceToSegment(Vector2 a, Vector2 b, Vector2 point)
        {
            var segment = b - a;
            var lengthSquared = segment.sqrMagnitude;
            var t = lengthSquared > 0f ? Mathf.Clamp01(Vector2.Dot(point - a, segment) / lengthSquared) : 0f;
            return (a + segment * t - point).magnitude;
        }
    }
}
