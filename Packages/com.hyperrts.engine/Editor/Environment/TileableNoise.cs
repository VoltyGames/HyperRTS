using UnityEngine;

namespace HyperRTS.Editor.Environment
{
    /// <summary>Gradient noise and fBm that repeat seamlessly every <c>period</c> lattice cells.</summary>
    public static class TileableNoise
    {
        /// <summary>Octaves of gradient noise, each twice as fine and half as strong; roughly -1..1.</summary>
        public static float Fbm(Vector2 position, int period, int octaves, int seed)
        {
            var sum = 0f;
            var total = 0f;
            var amplitude = 1f;
            for (var octave = 0; octave < octaves; octave++)
            {
                // Gradient noise is zero on its lattice; shifting each octave keeps those zeros from lining up into a
                // visible grid (any constant shift still tiles).
                var shift = new Vector2(0.618f, 0.382f) * octave;
                sum += Gradient(position + shift, period, seed + octave * 1013) * amplitude;
                total += amplitude;
                position *= 2f;
                period *= 2;
                amplitude *= 0.5f;
            }

            return sum / total;
        }

        /// <summary>Perlin gradient noise at a point in lattice cells; roughly -1..1.</summary>
        public static float Gradient(Vector2 position, int period, int seed)
        {
            var x = Mathf.FloorToInt(position.x);
            var y = Mathf.FloorToInt(position.y);
            var local = position - new Vector2(x, y);
            var bottom = Mathf.Lerp(Corner(x, y, local, period, seed), Corner(x + 1, y, local - Vector2.right, period, seed),
                Fade(local.x));
            var top = Mathf.Lerp(Corner(x, y + 1, local - Vector2.up, period, seed),
                Corner(x + 1, y + 1, local - Vector2.one, period, seed), Fade(local.x));
            return Mathf.Lerp(bottom, top, Fade(local.y)) * Mathf.Sqrt(2f);
        }

        /// <summary>Box-blurs a square field in place twice (close to a Gaussian), wrapping around so it still tiles.</summary>
        public static void Blur(float[] values, int size, int radius)
        {
            var scratch = new float[values.Length];
            for (var pass = 0; pass < 2; pass++)
            {
                BlurLines(values, scratch, size, radius, 1, size);
                BlurLines(scratch, values, size, radius, size, 1);
            }
        }

        /// <summary>The value at <paramref name="fraction"/> (0..1) of the way through the sorted values.</summary>
        public static float Percentile(float[] values, float fraction)
        {
            var sorted = (float[])values.Clone();
            System.Array.Sort(sorted);
            return sorted[Mathf.Clamp(Mathf.RoundToInt(fraction * (sorted.Length - 1)), 0, sorted.Length - 1)];
        }

        /// <summary>A running box sum along each line; <paramref name="step"/> walks a line, <paramref name="stride"/> picks the next one.</summary>
        private static void BlurLines(float[] source, float[] target, int size, int radius, int step, int stride)
        {
            var width = radius * 2 + 1;
            for (var line = 0; line < size; line++)
            {
                var start = line * stride;
                var sum = 0f;
                for (var i = -radius; i <= radius; i++)
                {
                    sum += source[start + Wrap(i, size) * step];
                }

                for (var i = 0; i < size; i++)
                {
                    target[start + i * step] = sum / width;
                    sum += source[start + Wrap(i + radius + 1, size) * step] - source[start + Wrap(i - radius, size) * step];
                }
            }
        }

        private static float Corner(int x, int y, Vector2 offset, int period, int seed)
        {
            var angle = Hash(Wrap(x, period), Wrap(y, period), seed) * (Mathf.PI * 2f / uint.MaxValue);
            return Mathf.Cos(angle) * offset.x + Mathf.Sin(angle) * offset.y;
        }

        private static uint Hash(int x, int y, int seed)
        {
            unchecked
            {
                var hash = (uint)x * 0x8DA6B343u ^ (uint)y * 0xD8163841u ^ (uint)seed * 0xCB1AB31Fu;
                hash ^= hash >> 13;
                hash *= 0x5BD1E995u;
                return hash ^ (hash >> 15);
            }
        }

        private static int Wrap(int value, int period) => (value % period + period) % period;

        private static float Fade(float t) => t * t * t * (t * (t * 6f - 15f) + 10f);
    }
}
