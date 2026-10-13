using HyperRTS.Editor.Maps.Data;
using HyperRTS.Editor.Maps.Geometry;
using HyperRTS.Editor.Maps.Land;
using HyperRTS.Editor.Maps.Layout;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Ground
{
    /// <summary>
    /// Paints the terrain layers from the land use: each texel blends the two surfaces of its nearest border across
    /// that border's band (<see cref="MapEdgeField"/>), with a surface's edge strip just inside its border (the terrain
    /// shader's height blend sharpens them naturally); open meadow gets patches of its variation, and steep ground its
    /// slope surface.
    /// </summary>
    public sealed class MapSplatPainter
    {
        private readonly MapLayout _layout;
        private readonly MapHeights _heights;
        private readonly MapClimate _climate;
        private readonly MapNoise _patches;
        private readonly MapEdgeField _edges;
        private readonly int[] _regionLayer;
        private readonly int[] _stripLayer;
        private readonly float[] _stripWidth;
        private readonly int _variation;
        private readonly int _slope;
        private readonly int _ground;

        private MapSplatPainter(MapLayout layout, MapLand land, MapHeights heights, MapClimate climate)
        {
            _layout = layout;
            _heights = heights;
            _climate = climate;
            _patches = new MapNoise(layout.Symmetry, layout.Spec.seed, MapNoiseChannel.SplatPatches);
            _edges = new MapEdgeField(layout, land, climate);
            var regions = land.Regions.Count;
            (_regionLayer, _stripLayer, _stripWidth) = (new int[regions], new int[regions], new float[regions]);
            for (var i = 0; i < regions; i++)
            {
                var surface = _edges.Surface(i);
                _regionLayer[i] = climate.LayerIndex(surface.layer);
                var hasStrip = !string.IsNullOrEmpty(surface.edgeSurface);
                _stripLayer[i] = hasStrip ? climate.SurfaceLayer(surface.edgeSurface) : -1;
                _stripWidth[i] = surface.edgeSurfaceWidth;
            }

            _variation = climate.SurfaceLayer(climate.groundVariation);
            _slope = climate.SurfaceLayer(climate.slopeSurface);
            _ground = climate.SurfaceLayer(climate.groundSurface);
        }

        /// <summary>Layer weights as Unity's alphamaps want them: [z, x, layer], summing to 1 per texel.</summary>
        public static float[,,] Paint(MapLayout layout, MapLand land, MapHeights heights, MapClimate climate,
            int resolution)
        {
            var splat = new float[resolution, resolution, climate.layers.Count];
            new MapSplatPainter(layout, land, heights, climate).Paint(splat, resolution);
            return splat;
        }

        private void Paint(float[,,] splat, int resolution)
        {
            var layers = _climate.layers.Count;
            var weights = new float[layers];
            var area = _layout.TerrainBounds;
            var texel = area.size / resolution;
            for (var z = 0; z < resolution; z++)
            {
                for (var x = 0; x < resolution; x++)
                {
                    var p = area.min + Vector2.Scale(new Vector2(x + 0.5f, z + 0.5f), texel);
                    System.Array.Clear(weights, 0, layers);
                    Surfaces(weights, p);
                    var steepness = (_heights.Slope(p) - _climate.slopeStart) / (_climate.slopeFull - _climate.slopeStart);
                    Over(weights, _slope, MapGeometry.Smooth(steepness));
                    for (var l = 0; l < layers; l++)
                    {
                        splat[z, x, l] = weights[l];
                    }
                }
            }
        }

        /// <summary>Splits the texel between the two sides of its nearest border.</summary>
        private void Surfaces(float[] weights, Vector2 p)
        {
            var edge = _edges.Sample(p);
            var share = edge.OwnShare;
            Side(weights, edge, edge.Own, share);
            if (share < 1f)
            {
                Side(weights, edge, edge.Other, 1f - share);
            }
        }

        /// <summary>One side's share, with its edge strip taking the part next to the border.</summary>
        private void Side(float[] weights, MapEdgeSample edge, int region, float amount)
        {
            var strip = _stripLayer[region];
            if (strip >= 0)
            {
                var stripShare = amount * edge.StripShare(region, _stripWidth[region]);
                Layer(weights, strip, stripShare, edge.Point);
                amount -= stripShare;
            }

            Layer(weights, _regionLayer[region], amount, edge.Point);
        }

        /// <summary>Open ground may turn to its variation.</summary>
        private void Layer(float[] weights, int layer, float amount, Vector2 p)
        {
            if (layer != _ground)
            {
                weights[layer] += amount;
                return;
            }

            var patch = Patch(p);
            weights[_variation] += amount * patch;
            weights[layer] += amount * (1f - patch);
        }

        /// <summary>Noise patches covering about <see cref="MapClimate.variationCoverage"/> of open ground.</summary>
        private float Patch(Vector2 p)
        {
            var threshold = 1f - 2f * _climate.variationCoverage;
            return MapGeometry.Smooth((_patches.Sample(p, 40f) - threshold + 0.15f) / 0.3f);
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
