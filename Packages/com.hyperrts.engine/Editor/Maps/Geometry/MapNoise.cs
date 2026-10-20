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
        private static readonly Vector2 SecondComponentOffset = new(311f, 173f);
        private readonly MapSymmetry _symmetry;
        private readonly Vector2 _offset;
        private readonly float _gain;

        public MapNoise(MapSymmetry symmetry, int seed, MapNoiseChannel channel)
        {
            _symmetry = symmetry;
            var random = MapRandom.Stream(seed, MapRandomChannel.Noise, (int)channel);
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

        /// <summary>
        /// A noise vector up to 1 long that turns with the symmetry (each copy's sample is turned back), so offsets
        /// built from it move mirrored points to mirrored places.
        /// </summary>
        public Vector2 SampleVector(Vector2 point, float wavelength)
        {
            var sum = Vector2.zero;
            for (var c = 0; c < _symmetry.Copies.Count; c++)
            {
                var copy = _symmetry.Copies[c];
                var p = copy.Apply(point) / wavelength;
                sum += copy.ApplyInverse(new Vector2(Fractal(p), Fractal(p + SecondComponentOffset)));
            }

            return Vector2.ClampMagnitude(sum / _symmetry.Copies.Count * _gain, 1f);
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
