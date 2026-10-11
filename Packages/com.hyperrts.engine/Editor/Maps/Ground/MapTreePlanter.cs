using System.Collections.Generic;
using System.Linq;
using HyperRTS.Editor.Maps.Data;
using HyperRTS.Editor.Maps.Geometry;
using HyperRTS.Editor.Maps.Layout;
using HyperRTS.Editor.Maps.Placement;
using HyperRTS.Editor.Maps.Spec;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Ground
{
    /// <summary>
    /// Plants the climate's trees: area surfaces (forests, orchards), ravine woods, willows along rivers, tree rows
    /// (shelterbelts) and garden trees. Trees are chosen in the authored sector and copied by the symmetry.
    /// </summary>
    public sealed class MapTreePlanter
    {
        private const int Attempts = 6;
        private const float BandOutside = 4f;
        private readonly MapLayout _layout;
        private readonly MapClimate _climate;
        private readonly MapTreeSpacing _spacing;
        private readonly MapIssues _issues;
        private readonly List<MapTree> _trees = new();

        private MapTreePlanter(MapLayout layout, MapShapes shapes, MapHeights heights, MapClimate climate,
            MapPlacer placer, MapIssues issues)
        {
            _layout = layout;
            _climate = climate;
            _issues = issues;
            _spacing = new MapTreeSpacing(shapes, heights, placer.Clearance);
        }

        public static List<MapTree> Plant(MapLayout layout, MapShapes shapes, MapHeights heights, MapClimate climate,
            MapPlacer placer, MapIssues issues)
        {
            var planter = new MapTreePlanter(layout, shapes, heights, climate, placer, issues);
            planter.PlantRows();
            planter.PlantYards(placer.Yards);
            planter.PlantAreas();
            planter.PlantBands(layout.Ravines, climate.ravineSurface, r => r.Authored.points, r => r.Authored.width * 0.5f + BandOutside, 0f, 20);
            planter.PlantBands(layout.Rivers, climate.shoreSurface, r => r.Authored.points,
                r => r.Authored.width * 0.5f + climate.shoreWidth + BandOutside, r => r.Authored.width * 0.5f + 1f, 30);
            return planter._trees;
        }

        private void PlantAreas()
        {
            foreach (var area in _layout.Areas.Where(a => a.Copy == 0))
            {
                var surface = _climate.Surface(area.Authored.kind);
                if (surface == null || surface.trees.Count == 0)
                {
                    continue;
                }

                var polygon = MapGeometry.ToVectors(area.Authored.points);
                var bounds = MapGeometry.Bounds(polygon);
                var hectares = MapGeometry.Area(polygon) / 10000f;
                var random = MapPlacer.Random(_layout.Spec.seed, 10, area.Index);
                Scatter(surface, hectares, random, () => RandomIn(bounds, random), p => MapGeometry.Contains(polygon, p),
                    (p, tree, scale, yaw) => AddCopies(_layout.Areas, area.Index, tree, p, scale, yaw));
            }
        }

        /// <summary>Trees of a surface in a band either side of authored polylines (ravines, river banks).</summary>
        private void PlantBands<T>(List<MapCopy<T>> features, string surfaceKey, System.Func<MapCopy<T>, List<MapPoint>> points,
            System.Func<MapCopy<T>, float> outer, System.Func<MapCopy<T>, float> inner, int channel) where T : class
        {
            var surface = _climate.Surface(surfaceKey);
            if (surface == null || surface.trees.Count == 0)
            {
                return;
            }

            foreach (var feature in features.Where(f => f.Copy == 0))
            {
                var line = MapGeometry.ToVectors(points(feature));
                var reach = outer(feature);
                var near = inner(feature);
                var bounds = MapGeometry.Bounds(line, reach);
                var hectares = MapGeometry.Length(line) * (reach - near) * 2f / 10000f;
                var random = MapPlacer.Random(_layout.Spec.seed, channel, feature.Index);
                Scatter(surface, hectares, random, () => RandomIn(bounds, random), p =>
                {
                    var distance = MapGeometry.DistanceToPolyline(line, p);
                    return distance <= reach && distance >= near;
                }, (p, tree, scale, yaw) => AddCopies(features, feature.Index, tree, p, scale, yaw));
            }
        }

        private void PlantBands<T>(List<MapCopy<T>> features, string surfaceKey, System.Func<MapCopy<T>, List<MapPoint>> points,
            System.Func<MapCopy<T>, float> outer, float inner, int channel) where T : class =>
            PlantBands(features, surfaceKey, points, outer, _ => inner, channel);

        private void PlantRows()
        {
            foreach (var line in _layout.Lines.Where(l => l.Copy == 0))
            {
                var tree = _climate.TreeIndex(line.Authored.kind);
                if (tree < 0)
                {
                    continue;
                }

                var points = MapGeometry.ToVectors(line.Authored.points);
                var length = MapGeometry.Length(points);
                var step = Mathf.Max(2f, line.Authored.spacing);
                var random = MapPlacer.Random(_layout.Spec.seed, 11, line.Index);
                for (var along = step * 0.5f; along < length; along += step)
                {
                    var p = MapGeometry.PointAlong(points, along + Jitter(random, step * 0.2f), out var direction);
                    p += new Vector2(direction.y, -direction.x) * Jitter(random, 0.6f);
                    if (_spacing.Fits(p, step * 0.5f))
                    {
                        AddCopies(_layout.Lines, line.Index, tree, p, Scale(tree, random), (float)random.NextDouble() * 360f);
                    }
                }
            }
        }

        private void PlantYards(List<Vector2> yards)
        {
            var tree = string.IsNullOrEmpty(_climate.gardenTree) ? -1 : _climate.TreeIndex(_climate.gardenTree);
            if (tree < 0)
            {
                return;
            }

            var random = MapPlacer.Random(_layout.Spec.seed, 12, 0);
            foreach (var yard in yards.Where(y => _spacing.Fits(y, 3f)))
            {
                _spacing.Add(yard);
                _trees.Add(new MapTree(tree, yard, Scale(tree, random), (float)random.NextDouble() * 360f));
            }
        }

        private void Scatter(MapClimateSurface surface, float hectares, System.Random random, System.Func<Vector2> candidate,
            System.Func<Vector2, bool> inside, System.Action<Vector2, int, float, float> add)
        {
            foreach (var density in surface.trees)
            {
                var tree = _climate.TreeIndex(density.tree);
                if (tree < 0)
                {
                    _issues.Error($"Surface '{surface.key}' grows tree '{density.tree}', which climate '{_climate.name}' lacks.");
                    continue;
                }

                var count = Mathf.RoundToInt(density.perHectare * hectares);
                for (var attempt = 0; attempt < count * Attempts && count > 0; attempt++)
                {
                    var p = candidate();
                    if (inside(p) && _spacing.Fits(p, surface.treeSpacing))
                    {
                        add(p, tree, Scale(tree, random), (float)random.NextDouble() * 360f);
                        count--;
                        attempt = Mathf.Max(0, attempt - Attempts);
                    }
                }
            }
        }

        private void AddCopies<T>(List<MapCopy<T>> features, int index, int tree, Vector2 position, float scale, float yaw)
            where T : class
        {
            foreach (var copy in features.Where(f => f.Index == index))
            {
                var placed = copy.Transform.Apply(position);
                _spacing.Add(placed);
                _trees.Add(new MapTree(tree, placed, scale, copy.Transform.ApplyYaw(yaw)));
            }
        }

        private float Scale(int tree, System.Random random)
        {
            var range = _climate.trees[tree].scale;
            return Mathf.Lerp(range.x, range.y, (float)random.NextDouble());
        }

        private static Vector2 RandomIn(Rect bounds, System.Random random) =>
            new(Mathf.Lerp(bounds.xMin, bounds.xMax, (float)random.NextDouble()),
                Mathf.Lerp(bounds.yMin, bounds.yMax, (float)random.NextDouble()));

        private static float Jitter(System.Random random, float amplitude) => ((float)random.NextDouble() * 2f - 1f) * amplitude;
    }
}
