using System.Collections.Generic;
using HyperRTS.Editor.Maps.Data;
using HyperRTS.Editor.Maps.Geometry;
using HyperRTS.Editor.Maps.Layout;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Ground
{
    /// <summary>
    /// Paints the terrain layers: open ground patched with its variation, then areas, ravines, shores, steep ground
    /// and roads, each laid over what is below with soft, noisy edges.
    /// </summary>
    public sealed class MapSplatPainter
    {
        private readonly MapLayout _layout;
        private readonly MapShapes _shapes;
        private readonly MapHeights _heights;
        private readonly MapClimate _climate;
        private readonly MapIssues _issues;
        private readonly MapNoise _patches;
        private readonly MapNoise _edges;
        private readonly List<(List<Vector2> Polygon, Rect Bounds, int Layer)> _areas = new();
        private readonly List<(MapLine Line, int Layer)> _roads = new();

        private MapSplatPainter(MapLayout layout, MapShapes shapes, MapHeights heights, MapClimate climate,
            MapIssues issues)
        {
            _layout = layout;
            _shapes = shapes;
            _heights = heights;
            _climate = climate;
            _issues = issues;
            _patches = new MapNoise(layout.Symmetry, layout.Spec.seed, 2);
            _edges = new MapNoise(layout.Symmetry, layout.Spec.seed, 3);
        }

        /// <summary>Layer weights as Unity's alphamaps want them: [z, x, layer], summing to 1 per texel.</summary>
        public static float[,,] Paint(MapLayout layout, MapShapes shapes, MapHeights heights, MapClimate climate,
            int resolution, MapIssues issues)
        {
            var painter = new MapSplatPainter(layout, shapes, heights, climate, issues);
            return painter.Paint(resolution);
        }

        private float[,,] Paint(int resolution)
        {
            var ground = Layer(_climate.groundSurface, "ground surface");
            var variation = Layer(_climate.groundVariation, "ground variation");
            var ravine = Layer(_climate.ravineSurface, "ravine surface");
            var shore = Layer(_climate.shoreSurface, "shore surface");
            var slope = Layer(_climate.slopeSurface, "slope surface");
            Collect();
            var layers = _climate.layers.Count;
            var splat = new float[resolution, resolution, layers];
            var weights = new float[layers];
            if (_issues.HasErrors)
            {
                return splat;
            }

            var area = _layout.TerrainBounds;
            var texel = area.size / resolution;
            for (var z = 0; z < resolution; z++)
            {
                for (var x = 0; x < resolution; x++)
                {
                    var p = area.min + Vector2.Scale(new Vector2(x + 0.5f, z + 0.5f), texel);
                    System.Array.Clear(weights, 0, layers);
                    weights[ground] = 1f;
                    Over(weights, variation, Patch(p));
                    PaintAreas(weights, p);
                    Over(weights, ravine, RavineWeight(p));
                    Over(weights, shore, 1f - MapGeometry.Smooth((_shapes.RiverEdgeDistance(p) + Edge(p, 1.5f)) / _climate.shoreWidth));
                    var steepness = (_heights.Slope(p) - _climate.slopeStart) / (_climate.slopeFull - _climate.slopeStart);
                    Over(weights, slope, MapGeometry.Smooth(steepness));
                    PaintRoads(weights, p);
                    for (var l = 0; l < layers; l++)
                    {
                        splat[z, x, l] = weights[l];
                    }
                }
            }

            return splat;
        }

        private void Collect()
        {
            foreach (var area in _layout.Areas)
            {
                var layer = Layer(area.Placed.kind, $"area {area.Index + 1}");
                var polygon = MapGeometry.ToVectors(area.Placed.points);
                _areas.Add((polygon, MapGeometry.Bounds(polygon, 2f), layer));
            }

            foreach (var road in _shapes.Roads)
            {
                _roads.Add((road, Layer(road.Kind, "road")));
            }
        }

        private void PaintAreas(float[] weights, Vector2 p)
        {
            foreach (var (polygon, bounds, layer) in _areas)
            {
                if (bounds.Contains(p))
                {
                    var inside = MapGeometry.SignedDistance(polygon, p) + Edge(p, 1.5f);
                    Over(weights, layer, MapGeometry.Smooth((inside + 0.75f) / 1.5f));
                }
            }
        }

        private void PaintRoads(float[] weights, Vector2 p)
        {
            foreach (var (road, layer) in _roads)
            {
                var edge = road.Distance(p) - road.Width * 0.5f + Edge(p, 0.4f);
                Over(weights, layer, 1f - MapGeometry.Smooth((edge + 0.5f) / 1f));
            }
        }

        private float RavineWeight(Vector2 p)
        {
            var weight = 0f;
            foreach (var (line, _) in _shapes.Ravines)
            {
                var edge = line.Distance(p) - line.Width * 0.5f + Edge(p, 2f);
                weight = Mathf.Max(weight, 1f - MapGeometry.Smooth((edge + 1f) / 3f));
            }

            return weight;
        }

        /// <summary>Noise patches covering about <see cref="MapClimate.variationCoverage"/> of open ground.</summary>
        private float Patch(Vector2 p)
        {
            var threshold = 1f - 2f * _climate.variationCoverage;
            return MapGeometry.Smooth((_patches.Sample(p, 40f) - threshold + 0.15f) / 0.3f);
        }

        private float Edge(Vector2 p, float amplitude) => _edges.Sample(p, 6f) * amplitude;

        private int Layer(string surface, string source)
        {
            var entry = _climate.Surface(surface);
            var index = entry != null ? _climate.LayerIndex(entry.layer) : -1;
            if (index < 0)
            {
                _issues.Error($"{source}: surface '{surface}' has no layer in climate '{_climate.name}'.");
                return 0;
            }

            return index;
        }

        private static void Over(float[] weights, int layer, float alpha)
        {
            if (alpha <= 0f)
            {
                return;
            }

            for (var i = 0; i < weights.Length; i++)
            {
                weights[i] *= 1f - alpha;
            }

            weights[layer] += alpha;
        }
    }
}
