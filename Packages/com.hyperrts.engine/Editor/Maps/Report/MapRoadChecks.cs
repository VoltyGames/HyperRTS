using System.Collections.Generic;
using System.Globalization;
using HyperRTS.Editor.Maps.Geometry;
using HyperRTS.Editor.Maps.Layout;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Report
{
    /// <summary>
    /// Warnings about the authored roads in the built map: dead ends (an end that meets no road, the map edge, the
    /// symmetry seam or a settlement) and bends tighter than half their class's curve radius. A feature found in
    /// every copy of the sector is reported once, at its authored-sector place.
    /// </summary>
    public static class MapRoadChecks
    {
        private const float SeamTolerance = 1f;
        private const float Touch = 1f;
        private const float BendStep = 3f;
        private const float BendShare = 0.5f;

        public static void Check(MapLayout layout, MapShapes shapes, MapIssues issues)
        {
            var reported = new HashSet<string>();
            foreach (var road in shapes.Roads)
            {
                if (road.Generated)
                {
                    continue;
                }

                var points = road.Line.Points;
                CheckEnd(layout, shapes, road, points[0], reported, issues);
                CheckEnd(layout, shapes, road, points[^1], reported, issues);
                CheckBends(layout, road, reported, issues);
            }
        }

        private static void CheckEnd(MapLayout layout, MapShapes shapes, MapRoad road, Vector2 end,
            HashSet<string> reported, MapIssues issues)
        {
            if (!layout.Bounds.Contains(end) || MapGeometry.OnEdge(end, layout.Bounds))
            {
                return;
            }

            var sector = layout.Symmetry.ToSector(end, out _);
            var onSeam = layout.Symmetry.SeamDistance(sector) < SeamTolerance;
            if (onSeam || InSettlement(layout, end))
            {
                return;
            }

            if (MeetsRoad(shapes, road, end))
            {
                return;
            }

            if (reported.Add($"end {sector.x:0},{sector.y:0}"))
            {
                issues.Warn($"Road '{road.Class.key}' dead-ends at {At(sector)}: join it to a road, run it to the map edge or the seam, or end it in a settlement.");
            }
        }

        private static void CheckBends(MapLayout layout, MapRoad road, HashSet<string> reported, MapIssues issues)
        {
            var points = MapCurves.Resample(road.Line.Points, BendStep);
            var tightest = float.PositiveInfinity;
            var where = Vector2.zero;
            for (var i = 1; i + 1 < points.Count; i++)
            {
                var radius = MapFillet.Circumradius(points[i - 1], points[i], points[i + 1]);
                if (radius < tightest)
                {
                    tightest = radius;
                    where = points[i];
                }
            }

            var limit = road.Class.curve * BendShare;
            if (tightest >= limit)
            {
                return;
            }

            var sector = layout.Symmetry.ToSector(where, out _);
            if (reported.Add($"bend {sector.x:0},{sector.y:0}"))
            {
                issues.Warn($"Road '{road.Class.key}' bends with a {tightest:0.#} m radius at {At(sector)}, under half its class radius ({road.Class.curve:0.#} m).");
            }
        }

        private static bool MeetsRoad(MapShapes shapes, MapRoad road, Vector2 end)
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

        private static string At(Vector2 point) => string.Format(CultureInfo.InvariantCulture, "({0:0}, {1:0})", point.x, point.y);
    }
}
