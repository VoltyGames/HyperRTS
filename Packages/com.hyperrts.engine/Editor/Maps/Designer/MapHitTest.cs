using System.Collections.Generic;
using HyperRTS.Editor.Maps.Spec;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Designer
{
    /// <summary>
    /// Finds the feature under the cursor on the Map Editor canvas: the topmost
    /// point, line or outline, else the topmost polygon or circle interior.
    /// </summary>
    public static class MapHitTest
    {
        public const float PointPixels = 7f;
        public const float LinePixels = 5f;

        private static readonly List<Vector2> Points = new();

        /// <summary>The handles of a feature in map metres: its points, plus a radius handle east of a circle.</summary>
        public static void Handles(MapSpec spec, MapFeatureRef feature, List<Vector2> into)
        {
            feature.Kind.ReadPoints(spec, feature.Index, into);
            if (feature.Kind.Geometry == MapFeatureGeometry.Circle)
            {
                into.Add(into[0] + new Vector2(feature.Kind.Radius(spec, feature.Index), 0f));
            }
        }

        public static MapFeatureRef FeatureAt(MapSpec spec, MapCanvasView view, Vector2 local)
        {
            var edge = Find(spec, view, local, false);
            return edge.IsNone ? Find(spec, view, local, true) : edge;
        }

        private static MapFeatureRef Find(MapSpec spec, MapCanvasView view, Vector2 local, bool interiors)
        {
            var world = view.ToWorld(local);
            var kinds = MapFeatureKinds.All;
            for (var k = kinds.Count - 1; k >= 0; k--)
            {
                var kind = kinds[k];
                for (var i = kind.Count(spec) - 1; i >= 0; i--)
                {
                    kind.ReadPoints(spec, i, Points);
                    var hit = interiors
                        ? Inside(kind, Points, kind.Radius(spec, i), world)
                        : OnEdge(kind, Points, kind.Radius(spec, i) * view.Scale, view, local);
                    if (hit)
                    {
                        return new MapFeatureRef(kind, i);
                    }
                }
            }

            return MapFeatureRef.None;
        }

        private static bool OnEdge(MapFeatureKind kind, List<Vector2> points, float radiusPixels, MapCanvasView view,
            Vector2 local)
        {
            switch (kind.Geometry)
            {
                case MapFeatureGeometry.Point:
                    return Vector2.Distance(view.ToLocal(points[0]), local) <= PointPixels;
                case MapFeatureGeometry.Circle:
                    var distance = Vector2.Distance(view.ToLocal(points[0]), local);
                    return distance <= PointPixels || Mathf.Abs(distance - radiusPixels) <= LinePixels;
                default:
                    return LineDistance(points, view, local, kind.Geometry == MapFeatureGeometry.Polygon) <= LinePixels;
            }
        }

        private static bool Inside(MapFeatureKind kind, List<Vector2> points, float radius, Vector2 world)
        {
            switch (kind.Geometry)
            {
                case MapFeatureGeometry.Circle:
                    return Vector2.Distance(points[0], world) <= radius;
                case MapFeatureGeometry.Polygon:
                    return InPolygon(points, world);
                default:
                    return false;
            }
        }

        private static float LineDistance(List<Vector2> points, MapCanvasView view, Vector2 local, bool closed)
        {
            var best = float.MaxValue;
            var segments = closed ? points.Count : points.Count - 1;
            for (var i = 0; i < segments; i++)
            {
                var a = view.ToLocal(points[i]);
                var b = view.ToLocal(points[(i + 1) % points.Count]);
                best = Mathf.Min(best, SegmentDistance(local, a, b));
            }

            return best;
        }

        public static float SegmentDistance(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            var lengthSquared = ab.sqrMagnitude;
            var t = lengthSquared > 0f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / lengthSquared) : 0f;
            return Vector2.Distance(p, a + ab * t);
        }

        /// <summary>Even-odd point in polygon.</summary>
        public static bool InPolygon(List<Vector2> polygon, Vector2 p)
        {
            var inside = false;
            for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
            {
                var (a, b) = (polygon[i], polygon[j]);
                var crosses = (a.y > p.y) != (b.y > p.y);
                if (crosses && p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y) + a.x)
                {
                    inside = !inside;
                }
            }

            return inside;
        }
    }
}
