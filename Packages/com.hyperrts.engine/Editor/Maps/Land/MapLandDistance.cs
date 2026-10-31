using System;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Land
{
    /// <summary>
    /// Distances on a grid to the nearest seed cell, spread from cell to neighbouring cell in two passes (within
    /// about 8% of the straight-line distance), for margins round fields and edge bands.
    /// </summary>
    public static class MapLandDistance
    {
        private const float Straight = 1f;
        private const float Diagonal = 1.4142f;

        /// <summary>Distance in cells from every cell to the nearest cell where <paramref name="seed"/> holds.</summary>
        public static float[] To(int width, int height, Func<int, int, bool> seed)
        {
            var distance = new float[width * height];
            for (var z = 0; z < height; z++)
            {
                for (var x = 0; x < width; x++)
                {
                    distance[z * width + x] = seed(x, z) ? 0f : float.MaxValue;
                }
            }

            Spread(width, height, (cell, from, step) => distance[cell] = Mathf.Min(distance[cell], distance[from] + step));
            return distance;
        }

        /// <summary>Distance from every cell to the nearest cell of another region (0 on a region's edge).</summary>
        public static float[] ToRegionEdge(int width, int height, Func<int, int, int> region) =>
            To(width, height, (x, z) => IsEdge(width, height, region, x, z));

        private static bool IsEdge(int width, int height, Func<int, int, int> region, int x, int z)
        {
            var own = region(x, z);
            if (x > 0 && region(x - 1, z) != own)
            {
                return true;
            }

            if (x < width - 1 && region(x + 1, z) != own)
            {
                return true;
            }

            if (z > 0 && region(x, z - 1) != own)
            {
                return true;
            }

            return z < height - 1 && region(x, z + 1) != own;
        }

        /// <summary>
        /// Spreads distances over a row-major grid, from the south-west corner and back from the north-east:
        /// <paramref name="carry"/> gets each cell, a neighbour this pass has already reached and the step between
        /// them (1 straight, 1.4142 diagonal), to carry that neighbour's distance over.
        /// </summary>
        public static void Spread(int width, int height, Action<int, int, float> carry)
        {
            for (var z = 0; z < height; z++)
            {
                for (var x = 0; x < width; x++)
                {
                    var i = z * width + x;
                    if (x > 0) carry(i, i - 1, Straight);
                    if (z > 0) carry(i, i - width, Straight);
                    if (x > 0 && z > 0) carry(i, i - width - 1, Diagonal);
                    if (x < width - 1 && z > 0) carry(i, i - width + 1, Diagonal);
                }
            }

            for (var z = height - 1; z >= 0; z--)
            {
                for (var x = width - 1; x >= 0; x--)
                {
                    var i = z * width + x;
                    if (x < width - 1) carry(i, i + 1, Straight);
                    if (z < height - 1) carry(i, i + width, Straight);
                    if (x < width - 1 && z < height - 1) carry(i, i + width + 1, Diagonal);
                    if (x > 0 && z < height - 1) carry(i, i + width - 1, Diagonal);
                }
            }
        }
    }
}
