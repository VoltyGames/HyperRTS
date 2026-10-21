using HyperRTS.Editor.Maps.Data;
using HyperRTS.Editor.Maps.Geometry;
using HyperRTS.Editor.Maps.Land;
using HyperRTS.Editor.Maps.Layout;
using HyperRTS.Editor.Maps.Symmetry;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Ground
{
    /// <summary>
    /// The terrain's ground tint at a map point. RGB 0.5 is neutral; A is wetness. Fields and woods each get their own
    /// tint and a slow gradient across them (strengths from the climate's <see cref="MapClimateGroundTint"/>), blended over
    /// the same border band as the ground textures; soil colour at 20 to 150 m vary light, warmth and moisture within them;
    /// 10 to 90 m noise breaks up everything; hollows read darker and damper, as do river banks and ravine floors.
    /// </summary>
    public sealed class MapGroundTint
    {
        private const float WetReach = 7f;

        /// <summary>Hollows: height curvature (1/m) measured over the span, from first sign to full damp.</summary>
        private const float HollowSpan = 8f;
        private const float HollowStart = 0.004f;
        private const float HollowFull = 0.014f;

        private readonly MapLand _land;
        private readonly MapShapes _shapes;
        private readonly MapHeights _heights;
        private readonly MapSymmetry _symmetry;
        private readonly MapClimateGroundTint _strength;
        private readonly MapEdgeField _edges;
        private readonly MapNoise _broad;
        private readonly MapNoise _detail;
        private readonly MapNoise _patches;
        private readonly MapNoise _moisture;
        private readonly Vector2[] _tint;
        private readonly Vector2[] _gradient;
        private readonly float[] _phase;

        public MapGroundTint(MapLayout layout, MapLand land, MapEdgeField edges, MapShapes shapes, MapHeights heights,
            MapClimateGroundTint strength)
        {
            (_land, _edges, _shapes, _heights, _symmetry) = (land, edges, shapes, heights, layout.Symmetry);
            _strength = strength;
            _broad = new MapNoise(layout.Symmetry, layout.Spec.seed, MapNoiseChannel.RegionTint);
            _detail = new MapNoise(layout.Symmetry, layout.Spec.seed, MapNoiseChannel.TintDetail);
            _patches = new MapNoise(layout.Symmetry, layout.Spec.seed, MapNoiseChannel.SoilColour);
            _moisture = new MapNoise(layout.Symmetry, layout.Spec.seed, MapNoiseChannel.SoilMoisture);
            (_tint, _gradient, _phase) = (new Vector2[land.Regions.Count], new Vector2[land.Regions.Count], new float[land.Regions.Count]);
            for (var i = 1; i < land.Regions.Count; i++)
            {
                if (Varies(land.Regions[i].Kind))
                {
                    RegionTint(i, MapRandom.Stream(layout.Spec.seed, MapRandomChannel.RegionTints, land.Regions[i].Seed));
                }
            }
        }

        public Color At(Vector2 p)
        {
            var edge = _edges.Sample(p);
            var share = edge.OwnShare;
            var tint = Tint(edge.Own, p) * share;
            if (share < 1f)
            {
                tint += Tint(edge.Other, p) * (1f - share);
            }

            var light = 0.5f + tint.x + 0.035f * _broad.Sample(p, 90f) + 0.02f * _detail.Sample(p, 14f) +
                        0.018f * _detail.Sample(p, 36f);
            var warmth = tint.y + 0.012f * _broad.Sample(p, 24f);
            var (soilLight, soilWarmth, soilWet) = Soil(p);
            light += soilLight;
            warmth += soilWarmth;
            var hollow = Hollow(p);
            light -= hollow * _strength.hollowDarkening;
            var wet = Mathf.Max(Mathf.Max(Wetness(edge.Region, p), hollow * _strength.hollowWetness), soilWet);
            return new Color(light + warmth, light, light - warmth, wet);
        }

        /// <summary>A region's own light and warmth shift, and the direction and phase of its gradient.</summary>
        private void RegionTint(int region, System.Random random)
        {
            _tint[region] = new Vector2((random.Value() - 0.5f) * _strength.regionLight,
                (random.Value() - 0.5f) * _strength.regionWarmth);
            var angle = random.Value() * Mathf.PI * 2f;
            _gradient[region] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (Mathf.PI * 2f / _strength.gradientLength);
            _phase[region] = random.Value() * Mathf.PI * 2f;
        }

        /// <summary>The gradient wave is averaged over the symmetry copies, so every copy of a field ripens alike.</summary>
        private Vector2 Tint(int region, Vector2 p)
        {
            if (_gradient[region] == Vector2.zero)
            {
                return _tint[region];
            }

            var wave = 0f;
            for (var c = 0; c < _symmetry.Copies.Count; c++)
            {
                wave += Mathf.Sin(Vector2.Dot(_symmetry.Copies[c].Apply(p), _gradient[region]) + _phase[region]);
            }

            var swing = new Vector2(_strength.gradientLight, _strength.gradientWarmth);
            return _tint[region] + swing * (wave / _symmetry.Copies.Count);
        }

        /// <summary>Soil colour: damp soil reads darker and cooler, so moisture and colour vary together.</summary>
        private (float Light, float Warmth, float Wet) Soil(Vector2 p)
        {
            var soil = _patches.Sample(p, _strength.soilSize);
            var damp = Mathf.Clamp01(_moisture.Sample(p, _strength.soilSize * 0.7f) * 1.6f);
            var light = _strength.soilLight * (soil - 0.5f * damp);
            var warmth = _strength.soilWarmth * (0.6f * soil - damp);
            return (light, warmth, damp * _strength.soilWetness);
        }

        /// <summary>0 on flat or convex ground, 1 in a clear hollow.</summary>
        private float Hollow(Vector2 p)
        {
            var across = new Vector2(HollowSpan, 0f);
            var along = new Vector2(0f, HollowSpan);
            var around = _heights.Height(p + across) + _heights.Height(p - across) + _heights.Height(p + along) +
                         _heights.Height(p - along);
            var curvature = (around - 4f * _heights.Height(p)) / (HollowSpan * HollowSpan);
            return MapGeometry.Smooth((curvature - HollowStart) / (HollowFull - HollowStart));
        }

        private float Wetness(int region, Vector2 p)
        {
            var near = 1f - MapGeometry.Smooth(_shapes.RiverEdgeDistance(p) / WetReach);
            var ravine = region > 0 && _land.Regions[region].Kind == MapLandKind.Ravine;
            return Mathf.Clamp01(Mathf.Max(near * 0.8f, ravine ? 0.25f : 0f));
        }

        private static bool Varies(MapLandKind kind) =>
            kind is MapLandKind.Field or MapLandKind.Garden or MapLandKind.Wood or MapLandKind.Back;
    }
}
