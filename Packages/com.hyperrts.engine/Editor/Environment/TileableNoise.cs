using UnityEngine;
using Random = Unity.Mathematics.Random;

namespace HyperRTS.Editor.Environment
{
    /// <summary>Gradient noise, fBm and lowpassed white noise that repeat seamlessly across their tile.</summary>
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

        /// <summary>
        /// White Gaussian noise with blobs about <paramref name="size"/> / <paramref name="cycles"/> pixels across,
        /// standardised to mean 0 and std 1.
        /// </summary>
        public static float[] Lowpass(int size, float cycles, ref Random random)
        {
            var values = new float[size * size];
            for (var i = 0; i < values.Length; i++)
            {
                values[i] = StandardNormal(ref random);
            }

            // This blur is the spatial form of the frequency filter exp(-(f / cycles)^2), f in cycles per tile.
            var kernel = GaussianKernel(size / (Mathf.Sqrt(2f) * Mathf.PI * cycles), size);
            var scratch = new float[values.Length];
            KernelLines(values, scratch, size, kernel, 1, size);
            KernelLines(scratch, values, size, kernel, size, 1);
            Standardise(values);
            return values;
        }

        /// <summary>Replaces each value with its rank spread evenly over 0..1, so the histogram is flat.</summary>
        public static void RankUniform(float[] values)
        {
            var keys = (float[])values.Clone();
            var order = new int[values.Length];
            for (var i = 0; i < order.Length; i++)
            {
                order[i] = i;
            }

            System.Array.Sort(keys, order);
            for (var rank = 0; rank < order.Length; rank++)
            {
                values[order[rank]] = (rank + 0.5f) / order.Length;
            }
        }

        /// <summary>Wrapping Sobel derivative along (du, dv) of a square field: [-1, 0, 1] along it, [1, 2, 1] across.</summary>
        public static float Sobel(float[] values, int size, int x, int y, int du, int dv)
        {
            var sum = 0f;
            for (var across = -1; across <= 1; across++)
            {
                var weight = across == 0 ? 2f : 1f;
                var ahead = values[Wrap(y + dv + across * du, size) * size + Wrap(x + du + across * dv, size)];
                var behind = values[Wrap(y - dv + across * du, size) * size + Wrap(x - du + across * dv, size)];
                sum += weight * (ahead - behind);
            }

            return sum;
        }

        private static float StandardNormal(ref Random random)
        {
            var radius = Mathf.Sqrt(-2f * Mathf.Log(1f - random.NextFloat()));
            return radius * Mathf.Cos(2f * Mathf.PI * random.NextFloat());
        }

        /// <summary>Gaussian weights for offsets -radius..radius, cut at four sigma or half the tile.</summary>
        private static float[] GaussianKernel(float sigma, int size)
        {
            var radius = Mathf.Min(Mathf.CeilToInt(4f * sigma), size / 2 - 1);
            var kernel = new float[radius * 2 + 1];
            for (var offset = -radius; offset <= radius; offset++)
            {
                kernel[offset + radius] = Mathf.Exp(-offset * offset / (2f * sigma * sigma));
            }

            return kernel;
        }

        private static void KernelLines(float[] source, float[] target, int size, float[] kernel, int step, int stride)
        {
            var radius = kernel.Length / 2;
            for (var line = 0; line < size; line++)
            {
                var start = line * stride;
                for (var i = 0; i < size; i++)
                {
                    var sum = 0f;
                    for (var k = 0; k < kernel.Length; k++)
                    {
                        sum += kernel[k] * source[start + Wrap(i + k - radius, size) * step];
                    }

                    target[start + i * step] = sum;
                }
            }
        }

        private static void Standardise(float[] values)
        {
            var mean = 0.0;
            foreach (var value in values)
            {
                mean += value;
            }

            mean /= values.Length;
            var variance = 0.0;
            foreach (var value in values)
            {
                variance += (value - mean) * (value - mean);
            }

            var std = System.Math.Sqrt(variance / values.Length);
            for (var i = 0; i < values.Length; i++)
            {
                values[i] = (float)((values[i] - mean) / std);
            }
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
