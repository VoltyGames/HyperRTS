using System.Linq;
using HyperRTS.Editor.Maps.Data;
using HyperRTS.Editor.Maps.Geometry;
using HyperRTS.Editor.Maps.Layout;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Ground
{
    /// <summary>
    /// Paints the terrain layers from the land use: each texel blends the two surfaces of its nearest border across
    /// that border's band (<see cref="MapEdgeField"/>; the terrain shader's height blend sharpens them naturally); each
    /// surface gets its noise-shaped mixes of other layers, and steep ground its slope surface.
    /// </summary>
    public sealed class MapGroundTexturePainter
    {
        private readonly MapLayout _layout;
        private readonly MapHeights _heights;
        private readonly MapClimate _climate;
        private readonly MapNoise[] _mixNoise;
        private readonly MapEdgeField _edges;
        private readonly int[] _regionLayer;
        private readonly MapSurfaceMix[][] _regionMixes;
        private readonly int[][] _regionMixLayers;
        private readonly int _slope;

        private MapGroundTexturePainter(MapLayout layout, MapEdgeField edges, MapHeights heights, MapClimate climate)
        {
            _layout = layout;
            _heights = heights;
            _climate = climate;
            _edges = edges;
            _regionLayer = new int[edges.RegionCount];
            _regionMixes = new MapSurfaceMix[edges.RegionCount][];
            _regionMixLayers = new int[edges.RegionCount][];
            for (var i = 0; i < _regionLayer.Length; i++)
            {
                var surface = edges.Surface(i);
                _regionLayer[i] = climate.LayerIndex(surface.layer);
                _regionMixes[i] = surface.mixes.ToArray();
                _regionMixLayers[i] = surface.mixes.Select(mix => climate.LayerIndex(mix.layer)).ToArray();
            }

            // One noise per mix layer, so every surface's mixes of a layer agree where surfaces meet.
            _mixNoise = new MapNoise[climate.layers.Count];
            for (var i = 0; i < _mixNoise.Length; i++)
            {
                _mixNoise[i] = new MapNoise(layout.Symmetry, layout.Spec.seed + 7919 * i, MapNoiseChannel.SurfaceMixes);
            }

            _slope = climate.SurfaceLayer(climate.slopeSurface);
        }

        /// <summary>Layer weights as Unity's alphamaps want them: [z, x, layer], summing to 1 per texel.</summary>
        public static float[,,] Paint(MapLayout layout, MapEdgeField edges, MapHeights heights, MapClimate climate,
            int resolution)
        {
            var splat = new float[resolution, resolution, climate.layers.Count];
            new MapGroundTexturePainter(layout, edges, heights, climate).Paint(splat, resolution);
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
            Region(weights, edge.Own, share, edge.Point);
            if (share < 1f)
            {
                Region(weights, edge.Other, 1f - share, edge.Point);
            }
        }

        /// <summary>The region's own layer, with its surface's mixes painted over it in order.</summary>
        private void Region(float[] weights, int region, float amount, Vector2 p)
        {
            var left = amount;
            var mixes = _regionMixes[region];
            for (var i = 0; i < mixes.Length; i++)
            {
                var layer = _regionMixLayers[region][i];
                var cover = left * Mix(layer, mixes[i], p);
                weights[layer] += cover;
                left -= cover;
            }

            weights[_regionLayer[region]] += left;
        }

        /// <summary>Noise blotches covering about <see cref="MapSurfaceMix.coverage"/> of the surface.</summary>
        private float Mix(int layer, MapSurfaceMix mix, Vector2 p)
        {
            // Measured: map noise sits about 0.2 either side of -0.1, so this threshold leaves roughly the coverage.
            var threshold = -0.1f + 0.72f * (0.5f - mix.coverage);
            return MapGeometry.Smooth((_mixNoise[layer].Sample(p, mix.size) - threshold) / 0.16f + 0.5f);
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
