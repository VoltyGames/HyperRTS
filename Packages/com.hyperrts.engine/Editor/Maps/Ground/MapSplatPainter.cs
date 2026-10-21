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
    /// surface gets its noise patches of other layers, and steep ground its slope surface.
    /// </summary>
    public sealed class MapSplatPainter
    {
        private readonly MapLayout _layout;
        private readonly MapHeights _heights;
        private readonly MapClimate _climate;
        private readonly MapNoise[] _patchNoise;
        private readonly MapEdgeField _edges;
        private readonly int[] _regionLayer;
        private readonly MapSurfacePatch[][] _regionPatches;
        private readonly int[][] _regionPatchLayers;
        private readonly int _slope;

        private MapSplatPainter(MapLayout layout, MapEdgeField edges, MapHeights heights, MapClimate climate)
        {
            _layout = layout;
            _heights = heights;
            _climate = climate;
            _edges = edges;
            _regionLayer = new int[edges.RegionCount];
            _regionPatches = new MapSurfacePatch[edges.RegionCount][];
            _regionPatchLayers = new int[edges.RegionCount][];
            for (var i = 0; i < _regionLayer.Length; i++)
            {
                var surface = edges.Surface(i);
                _regionLayer[i] = climate.LayerIndex(surface.layer);
                _regionPatches[i] = surface.patches.ToArray();
                _regionPatchLayers[i] = surface.patches.Select(patch => climate.LayerIndex(patch.layer)).ToArray();
            }

            // One noise per patch layer, so every surface's patches of a layer agree where surfaces meet.
            _patchNoise = new MapNoise[climate.layers.Count];
            for (var i = 0; i < _patchNoise.Length; i++)
            {
                _patchNoise[i] = new MapNoise(layout.Symmetry, layout.Spec.seed + 7919 * i, MapNoiseChannel.SplatPatches);
            }

            _slope = climate.SurfaceLayer(climate.slopeSurface);
        }

        /// <summary>Layer weights as Unity's alphamaps want them: [z, x, layer], summing to 1 per texel.</summary>
        public static float[,,] Paint(MapLayout layout, MapEdgeField edges, MapHeights heights, MapClimate climate,
            int resolution)
        {
            var splat = new float[resolution, resolution, climate.layers.Count];
            new MapSplatPainter(layout, edges, heights, climate).Paint(splat, resolution);
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

        /// <summary>The region's own layer, with its surface's patches painted over it in order.</summary>
        private void Region(float[] weights, int region, float amount, Vector2 p)
        {
            var left = amount;
            var patches = _regionPatches[region];
            for (var i = 0; i < patches.Length; i++)
            {
                var layer = _regionPatchLayers[region][i];
                var cover = left * Patch(layer, patches[i], p);
                weights[layer] += cover;
                left -= cover;
            }

            weights[_regionLayer[region]] += left;
        }

        /// <summary>Noise patches covering about <see cref="MapSurfacePatch.coverage"/> of the surface.</summary>
        private float Patch(int layer, MapSurfacePatch patch, Vector2 p)
        {
            // Measured: map noise sits about 0.2 either side of -0.1, so this threshold leaves roughly the coverage.
            var threshold = -0.1f + 0.72f * (0.5f - patch.coverage);
            return MapGeometry.Smooth((_patchNoise[layer].Sample(p, patch.size) - threshold) / 0.16f + 0.5f);
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
