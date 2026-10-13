using HyperRTS.Editor.Maps.Geometry;
using HyperRTS.Editor.Maps.Layout;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Ground
{
    /// <summary>
    /// Wander for land-use border lookups: a fine wave for small jitter turning into a slow one as the jitter grows, so
    /// field edges ripple and meadow edges meander.
    /// </summary>
    public sealed class MapEdgeJitter
    {
        private const float FineWavelength = 6f;
        private const float CoarseWavelength = 22f;

        /// <summary>Jitter at which the wander is all coarse wave (metres).</summary>
        private const float CoarseJitter = 3f;

        /// <summary>
        /// Fractal noise rarely strays past about a third of its range (each component's spread is near 0.17), so the
        /// wave is scaled up to make the jitter the line's usual farthest wander, not a bound it never nears.
        /// </summary>
        private const float TypicalPeak = 0.35f;

        private readonly MapNoise _noise;

        public MapEdgeJitter(MapLayout layout)
        {
            _noise = new MapNoise(layout.Symmetry, layout.Spec.seed, MapNoiseChannel.LandEdges);
        }

        /// <summary>Offset of up to <paramref name="amplitude"/> metres to look the land up at.</summary>
        public Vector2 Offset(Vector2 point, float amplitude)
        {
            var coarse = Mathf.Clamp01(amplitude / CoarseJitter);
            var offset = Vector2.zero;
            if (coarse < 1f)
            {
                offset += _noise.SampleVector(point, FineWavelength) * (1f - coarse);
            }

            if (coarse > 0f)
            {
                offset += _noise.SampleVector(point, CoarseWavelength) * coarse;
            }

            return Vector2.ClampMagnitude(offset / TypicalPeak, 1f) * amplitude;
        }
    }
}
