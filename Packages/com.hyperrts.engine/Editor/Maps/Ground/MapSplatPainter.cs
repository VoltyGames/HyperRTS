using HyperRTS.Editor.Maps.Data;
using HyperRTS.Editor.Maps.Geometry;
using HyperRTS.Editor.Maps.Land;
using HyperRTS.Editor.Maps.Layout;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Ground
{
    /// <summary>
    /// Paints the terrain layers from the land use: each texel averages the surfaces found at a few noise-jittered
    /// points round it, so borders come out soft and ragged (the terrain shader's height blend sharpens them
    /// naturally); open meadow gets patches of its variation, and steep ground its slope surface.
    /// </summary>
    public sealed class MapSplatPainter
    {
        private const float Spread = 0.55f;
        private static readonly Vector2[] Taps = { Vector2.zero, new(1f, 0.3f), new(-0.3f, 1f), new(-1f, -0.3f), new(0.3f, -1f) };

        private readonly MapLayout _layout;
        private readonly MapLand _land;
        private readonly MapHeights _heights;
        private readonly MapClimate _climate;
        private readonly MapNoise _patches;
        private readonly MapEdgeJitter _edges;
        private readonly int[] _regionLayer;
        private readonly int _variation;
        private readonly int _slope;
        private readonly int _ground;

        private MapSplatPainter(MapLayout layout, MapLand land, MapHeights heights, MapClimate climate)
        {
            _layout = layout;
            _land = land;
            _heights = heights;
            _climate = climate;
            _patches = new MapNoise(layout.Symmetry, layout.Spec.seed, MapNoiseChannel.SplatPatches);
            _edges = new MapEdgeJitter(layout);
            _regionLayer = new int[land.Regions.Count];
            for (var i = 1; i < land.Regions.Count; i++)
            {
                _regionLayer[i] = climate.SurfaceLayer(land.Regions[i].Surface);
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

        /// <summary>Averages the layers at jittered taps round the texel; meadow taps may turn to its variation.</summary>
        private void Surfaces(float[] weights, Vector2 p)
        {
            var offset = _edges.Offset(p);
            var share = 1f / Taps.Length;
            foreach (var tap in Taps)
            {
                var point = p + offset + tap * Spread;
                var layer = _land.TryCell(point, out var cx, out var cz) ? _regionLayer[_land.RegionIndex(cx, cz)] : _ground;
                if (layer == _ground)
                {
                    var patch = Patch(point);
                    weights[_variation] += share * patch;
                    weights[layer] += share * (1f - patch);
                    continue;
                }

                weights[layer] += share;
            }
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
