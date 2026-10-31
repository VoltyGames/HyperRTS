using System.Collections.Generic;
using HyperRTS.Editor.Maps.Geometry;
using HyperRTS.Editor.Maps.Spec;
using Unity.Scripting.LifecycleManagement;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Designer
{
    /// <summary>
    /// Finds what is under the cursor on the Map Editor canvas: a selected feature's handle, else the topmost
    /// point, line or outline, else the topmost polygon or circle interior.
    /// </summary>
    public static class MapHitTest
    {
        public const float PointPixels = 7f;
        public const float LinePixels = 5f;

        [NoAutoStaticsCleanup]
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

        /// <summary>The index of the handle of <paramref name="feature"/> under the cursor, or -1.</summary>
        public static int HandleAt(MapSpec spec, MapCanvasView view, MapFeatureRef feature, Vector2 local)
        {
            Handles(spec, feature, Points);
            for (var i = Points.Count - 1; i >= 0; i--)
            {
                if (Vector2.Distance(view.ToLocal(Points[i]), local) <= PointPixels)
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>The segment of a line or area under the cursor, or -1 (always -1 for points and circles).</summary>
        public static int SegmentAt(MapSpec spec, MapCanvasView view, MapFeatureRef feature, Vector2 local)
        {
            if (!feature.Kind.HasPoints)
            {
                return -1;
            }

            feature.Kind.ReadPoints(spec, feature.Index, Points);
            for (var i = 0; i < Points.Count; i++)
            {
                Points[i] = view.ToLocal(Points[i]);
            }

            var closed = feature.Kind.Geometry == MapFeatureGeometry.Polygon;
            return MapVertexRules.SegmentAt(Points, closed, local, LinePixels);
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
                    return MapGeometry.Contains(points, world);
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
                best = Mathf.Min(best, MapGeometry.DistanceToSegment(a, b, local));
            }

            return best;
        }
    }
}
