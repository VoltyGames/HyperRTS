using UnityEngine;

namespace HyperRTS.Presentation.Environment
{
    /// <summary>
    /// The rolling gust field: a tileable greyscale noise map (white = gust) and the downwind scroll of its offset.
    /// </summary>
    public static class WindGust
    {
        /// <summary>Lattice cells across one tile for the coarse and the fine octave.</summary>
        private const int CoarseCells = 4;
        private const int FineCells = 9;
        private const float FineWeight = 0.35f;

        /// <summary>
        /// Moves the offset (in tiles) <paramref name="tilesPerSecond"/> along <paramref name="downwind"/>, wrapped to
        /// [0, 1) so it never loses precision; the map repeats, so the wrap is invisible.
        /// </summary>
        public static Vector2 Scroll(Vector2 offset, Vector2 downwind, float tilesPerSecond, float deltaTime)
        {
            var moved = offset + downwind * (tilesPerSecond * deltaTime);
            return new Vector2(Mathf.Repeat(moved.x, 1f), Mathf.Repeat(moved.y, 1f));
        }

        /// <summary>Tileable value noise in [0, 1] at (u, v) in tiles: two octaves on lattices that wrap at 1.</summary>
        public static float Noise(float u, float v)
        {
            var coarse = ValueNoise(u, v, CoarseCells);
            var fine = ValueNoise(u, v, FineCells);
            return Mathf.Lerp(coarse, fine, FineWeight);
        }

        /// <summary>A size × size linear R8 map of <see cref="Noise"/>, stretched to the full 0..1 range.</summary>
        public static Texture2D CreateMap(int size)
        {
            var values = new float[size * size];
            var low = float.MaxValue;
            var high = float.MinValue;
            for (var i = 0; i < values.Length; i++)
            {
                var value = Noise((i % size + 0.5f) / size, (i / size + 0.5f) / size);
                values[i] = value;
                low = Mathf.Min(low, value);
                high = Mathf.Max(high, value);
            }

            var pixels = new byte[values.Length];
            for (var i = 0; i < values.Length; i++)
            {
                pixels[i] = (byte)Mathf.RoundToInt(Mathf.InverseLerp(low, high, values[i]) * 255f);
            }

            var map = new Texture2D(size, size, TextureFormat.R8, false, true)
            {
                name = "Wind Gusts",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.DontSave,
            };
            map.SetPixelData(pixels, 0);
            map.Apply(false);
            return map;
        }

        private static float ValueNoise(float u, float v, int cells)
        {
            var x = u * cells;
            var y = v * cells;
            var x0 = Mathf.FloorToInt(x);
            var y0 = Mathf.FloorToInt(y);
            var tx = Mathf.SmoothStep(0f, 1f, x - x0);
            var ty = Mathf.SmoothStep(0f, 1f, y - y0);
            var bottom = Mathf.Lerp(Lattice(x0, y0, cells), Lattice(x0 + 1, y0, cells), tx);
            var top = Mathf.Lerp(Lattice(x0, y0 + 1, cells), Lattice(x0 + 1, y0 + 1, cells), tx);
            return Mathf.Lerp(bottom, top, ty);
        }

        private static float Lattice(int x, int y, int cells)
        {
            unchecked
            {
                var hash = (uint)(Wrap(x, cells) * 73856093) ^ (uint)(Wrap(y, cells) * 19349663) ^ (uint)(cells * 83492791);
                hash ^= hash >> 13;
                hash *= 0x5bd1e995u;
                hash ^= hash >> 15;
                return (hash & 0xffffu) / 65535f;
            }
        }

        private static int Wrap(int index, int cells) => ((index % cells) + cells) % cells;
    }
}
