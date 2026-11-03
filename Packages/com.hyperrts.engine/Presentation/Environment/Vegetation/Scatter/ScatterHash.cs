using HyperRTS.Presentation.Common;
using UnityEngine;

namespace HyperRTS.Presentation.Environment.Vegetation.Scatter
{
    /// <summary>
    /// Scatter.hlsl's hashes and noise in C# for the CPU and tests: hashing seed and indices makes symmetric copies
    /// of a map match.
    /// </summary>
    public static class ScatterHash
    {
        public static uint Hash(int seed, int row, int plant) =>
            Pcg((uint)seed ^ Pcg((uint)row ^ Pcg((uint)plant)));

        /// <summary>An independent random value per channel from one plant hash.</summary>
        public static uint Channel(uint hash, uint channel) => Pcg(hash + channel * 0x9E3779B9u);

        /// <summary>A seed for an independent stream of values from the same map seed.</summary>
        public static int Salted(int seed, uint salt) => (int)((uint)seed ^ salt);

        /// <summary>Smooth value noise from 0 to 1 on an integer grid.</summary>
        public static float Noise(int seed, uint salt, Vector2 point)
        {
            var salted = Salted(seed, salt);
            var (x, z) = (Mathf.FloorToInt(point.x), Mathf.FloorToInt(point.y));
            var (fx, fz) = (point.x - x, point.y - z);
            var (ux, uz) = (fx * fx * (3f - 2f * fx), fz * fz * (3f - 2f * fz));
            var low = Mathf.Lerp(Hashes.Fraction(Hash(salted, x, z)), Hashes.Fraction(Hash(salted, x + 1, z)), ux);
            var high = Mathf.Lerp(Hashes.Fraction(Hash(salted, x, z + 1)), Hashes.Fraction(Hash(salted, x + 1, z + 1)), ux);
            return Mathf.Lerp(low, high, uz);
        }

        private static uint Pcg(uint value)
        {
            var state = value * 747796405u + 2891336453u;
            var word = ((state >> (int)((state >> 28) + 4u)) ^ state) * 277803737u;
            return (word >> 22) ^ word;
        }
    }
}
