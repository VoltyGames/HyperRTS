using System;
using System.Numerics;
using UnityEngine;
using Vector2 = UnityEngine.Vector2;
using Vector3 = UnityEngine.Vector3;
using Random = Unity.Mathematics.Random;

namespace HyperRTS.Editor.Environment
{
    /// <summary>
    /// Tiling ripple slopes from a wind-wave spectrum, summed by an inverse FFT so the normals tile exactly.
    /// </summary>
    public static class WaterSpectrum
    {
        /// <summary>Unit tangent-space normals, size × size, v up as Unity samples them.</summary>
        public static Vector3[] Normals(int size, uint seed, float peakCycles, float steepness)
        {
            var slopeU = new Complex[size * size];
            var slopeV = new Complex[size * size];
            var random = new Random(seed);
            var wind = new Vector2(0.8f, 0.6f);
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var k = new Vector2(Wrapped(x, size), Wrapped(y, size));
                    var height = Amplitude(k, wind, peakCycles, size) * TileableNoise.StandardNormal(ref random);
                    var phase = random.NextFloat(0f, 2f * Mathf.PI);
                    var wave = Complex.FromPolarCoordinates(height, phase);
                    // A slope is the height's derivative: i k times each wave.
                    slopeU[y * size + x] = wave * new Complex(0.0, k.x);
                    slopeV[y * size + x] = wave * new Complex(0.0, k.y);
                }
            }

            Fft.Inverse2D(slopeU, size);
            Fft.Inverse2D(slopeV, size);
            var scale = steepness / Rms(slopeU, slopeV);
            var normals = new Vector3[size * size];
            for (var i = 0; i < normals.Length; i++)
            {
                var u = (float)(-slopeU[i].Real * scale);
                var v = (float)(-slopeV[i].Real * scale);
                normals[i] = new Vector3(u, v, 1f).normalized;
            }

            return normals;
        }

        // Waves per tile along an axis, negative past the middle.
        private static float Wrapped(int index, int size) => index <= size / 2 ? index : index - size;

        // Phillips-like: few waves longer than the peak, falling as k^-5 shorter so slopes stay smooth rather than
        // grainy, a quarter of the energy across the wind, and nothing shorter than 1/150 of the tile.
        private static float Amplitude(Vector2 k, Vector2 wind, float peakCycles, int size)
        {
            var length = k.magnitude;
            if (length < 1f || length > Mathf.Min(150f, size * 0.375f))
            {
                return 0f;
            }

            var along = Vector2.Dot(k / length, wind);
            var spread = 0.25f + 0.75f * along * along;
            var spectrum = Mathf.Exp(-1f / Mathf.Pow(length / peakCycles, 2f)) / Mathf.Pow(length, 5f) * spread;
            return Mathf.Sqrt(spectrum);
        }

        private static double Rms(Complex[] u, Complex[] v)
        {
            double sum = 0;
            for (var i = 0; i < u.Length; i++)
            {
                sum += u[i].Real * u[i].Real + v[i].Real * v[i].Real;
            }

            return Math.Sqrt(sum / u.Length);
        }
    }
}
