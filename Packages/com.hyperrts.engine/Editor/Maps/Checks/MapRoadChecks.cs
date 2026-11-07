using System.Collections.Generic;
using System.Globalization;
using HyperRTS.Editor.Maps.Common;
using HyperRTS.Editor.Maps.Geometry;
using HyperRTS.Editor.Maps.Layout;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Checks
{
    /// <summary>
    /// Warnings about dead-end roads and bends tighter than half their class's curve radius. A feature found in every
    /// copy of the sector is reported once.
    /// </summary>
    public sealed class MapRoadChecks
    {
        private const float SeamTolerance = 1f;
        private const float BendStep = 3f;
        private const float BendShare = 0.5f;

        private readonly MapLayout _layout;
        private readonly MapShapes _shapes;
        private readonly float _baseRadius;
        private readonly MapIssues _issues;
        private readonly HashSet<string> _reported = new();

        private MapRoadChecks(MapLayout layout, MapShapes shapes, float baseRadius, MapIssues issues)
        {
            _layout = layout;
            _shapes = shapes;
            _baseRadius = baseRadius;
            _issues = issues;
        }

        public static void Check(MapLayout layout, MapShapes shapes, float baseRadius, MapIssues issues)
        {
            new MapRoadChecks(layout, shapes, baseRadius, issues).CheckRoads();
        }

        private void CheckRoads()
        {
            foreach (var road in _shapes.Roads)
            {
                if (road.Generated)
                {
                    continue;
                }

                var points = road.Line.Points;
                CheckEnd(road, points[0]);
                CheckEnd(road, points[^1]);
                CheckBends(road);
            }
        }

        private void CheckEnd(MapRoad road, Vector2 end)
        {
            var sector = _layout.Symmetry.ToSector(end, out _);
            var onSeam = _layout.Symmetry.SeamDistance(sector) < SeamTolerance;
            if (onSeam || MapRoadEnds.LeadsSomewhere(_layout, _shapes, road, end, _baseRadius))
            {
                return;
            }

            if (_reported.Add($"end {sector.x:0},{sector.y:0}"))
            {
                _issues.Warn($"Road '{road.Class.key}' dead-ends at {At(sector)}: join it to a road, run it to the map edge or the seam, or end it in a settlement.",
                    road.Feature);
            }
        }

        private void CheckBends(MapRoad road)
        {
            var points = MapCurves.Resample(road.Line.Points, BendStep);
            var tightest = float.PositiveInfinity;
            var where = Vector2.zero;
            for (var i = 1; i + 1 < points.Count; i++)
            {
                var radius = MapCornerRounding.Circumradius(points[i - 1], points[i], points[i + 1]);
                if (radius < tightest)
                {
                    tightest = radius;
                    where = points[i];
                }
            }

            var limit = road.Class.cornerRadius * BendShare;
            if (tightest >= limit)
            {
                return;
            }

            var sector = _layout.Symmetry.ToSector(where, out _);
            if (_reported.Add($"bend {sector.x:0},{sector.y:0}"))
            {
                _issues.Warn($"Road '{road.Class.key}' bends with a {tightest:0.#} m radius at {At(sector)}, under half its class radius ({road.Class.cornerRadius:0.#} m).",
                    road.Feature);
            }
        }

        private static string At(Vector2 point) => string.Format(CultureInfo.InvariantCulture, "({0:0}, {1:0})", point.x, point.y);
    }
}
