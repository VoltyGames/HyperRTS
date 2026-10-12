using HyperRTS.Editor.Maps.Symmetry;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Geometry
{
    /// <summary>
    /// Seeded fractal noise in -1..1, averaged over the symmetry's copies so it is exactly symmetric with no seam at
    /// the sector edges.
    /// </summary>
    public sealed class MapNoise
    {
        private const int Octaves = 4;
        private readonly MapSymmetry _symmetry;
        private readonly Vector2 _offset;
        private readonly float _gain;

        public MapNoise(MapSymmetry symmetry, int seed, MapNoiseChannel channel)
        {
            _symmetry = symmetry;
            var random = new System.Random(seed * 7919 + (int)channel * 104729);
            _offset = new Vector2(random.Value() * 10000f, random.Value() * 10000f);

            // Averaging n uncorrelated copies shrinks the spread by sqrt(n); scale it back.
            _gain = Mathf.Sqrt(symmetry.Copies.Count);
        }

        /// <summary>Noise at a map point with features about <paramref name="wavelength"/> metres apart.</summary>
        public float Sample(Vector2 point, float wavelength)
        {
            var sum = 0f;
            foreach (var copy in _symmetry.Copies)
            {
                sum += Fractal(copy.Apply(point) / wavelength);
            }

            return Mathf.Clamp(sum / _symmetry.Copies.Count * _gain, -1f, 1f);
        }

        private float Fractal(Vector2 point)
        {
            var sum = 0f;
            var amplitude = 0.5f;
            var frequency = 1f;
            for (var i = 0; i < Octaves; i++)
            {
                var p = point * frequency + _offset;
                sum += (Mathf.PerlinNoise(p.x, p.y) * 2f - 1f) * amplitude;
                amplitude *= 0.5f;
                frequency *= 2.03f;
            }

            return sum / 0.9375f;
        }
    }
}
