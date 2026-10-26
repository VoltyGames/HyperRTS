using System;
using System.IO;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Report
{
    /// <summary>How much light and shade a map view shows: the spread of its brightness and its local contrast.</summary>
    public readonly struct MapViewContrast
    {
        // Local contrast compares each cell of a 4 × 4 pixel grid with the 5 × 5 cells round it, about 1 m at view height.
        private const int Cell = 4;
        private const int Radius = 2;

        public MapViewContrast(float luminanceSpread, float localContrast)
        {
            LuminanceSpread = luminanceSpread;
            LocalContrast = localContrast;
        }

        /// <summary>The 5th to 95th percentile of luminance (0 to 1). Low means one value everywhere.</summary>
        public float LuminanceSpread { get; }

        /// <summary>Mean distance of each cell's luminance from its neighbourhood's (0 to 1). Low means flat ground.</summary>
        public float LocalContrast { get; }

        /// <summary>Measures a view PNG.</summary>
        public static MapViewContrast Load(string path)
        {
            var image = new Texture2D(2, 2, TextureFormat.RGB24, false);
            try
            {
                image.LoadImage(File.ReadAllBytes(path));
                return Measure(image.GetPixels32(), image.width, image.height);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(image);
            }
        }

        /// <summary>Measures display (sRGB) pixels, rows bottom up as Unity stores them.</summary>
        public static MapViewContrast Measure(Color32[] pixels, int width, int height)
        {
            var cells = Cells(pixels, width, height, out var columns, out var rows);
            var sorted = (float[])cells.Clone();
            Array.Sort(sorted);
            var spread = sorted[(int)(sorted.Length * 0.95f)] - sorted[(int)(sorted.Length * 0.05f)];
            return new MapViewContrast(spread, LocalDeviation(cells, columns, rows));
        }

        // Luminance averaged over Cell × Cell blocks, so texture noise reads as ground, not as contrast.
        private static float[] Cells(Color32[] pixels, int width, int height, out int columns, out int rows)
        {
            columns = width / Cell;
            rows = height / Cell;
            var cells = new float[columns * rows];
            for (var y = 0; y < rows * Cell; y++)
            {
                for (var x = 0; x < columns * Cell; x++)
                {
                    var p = pixels[y * width + x];
                    cells[y / Cell * columns + x / Cell] += (0.2126f * p.r + 0.7152f * p.g + 0.0722f * p.b) / 255f;
                }
            }

            for (var i = 0; i < cells.Length; i++)
            {
                cells[i] /= Cell * Cell;
            }

            return cells;
        }

        private static float LocalDeviation(float[] cells, int columns, int rows)
        {
            var total = 0.0;
            var count = 0;
            for (var y = Radius; y < rows - Radius; y++)
            {
                for (var x = Radius; x < columns - Radius; x++)
                {
                    total += Mathf.Abs(cells[y * columns + x] - Neighbourhood(cells, columns, x, y));
                    count++;
                }
            }

            return count == 0 ? 0f : (float)(total / count);
        }

        private static float Neighbourhood(float[] cells, int columns, int x, int y)
        {
            var sum = 0f;
            for (var dy = -Radius; dy <= Radius; dy++)
            {
                for (var dx = -Radius; dx <= Radius; dx++)
                {
                    sum += cells[(y + dy) * columns + x + dx];
                }
            }

            return sum / ((2 * Radius + 1) * (2 * Radius + 1));
        }
    }
}
