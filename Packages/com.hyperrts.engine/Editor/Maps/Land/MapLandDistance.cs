using System;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Land
{
    /// <summary>
    /// Distances on a grid to the nearest seed cell (a two-pass eight-neighbour chamfer, within about 8% of
    /// Euclidean), for margins round fields and edge bands.
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

            Forward(distance, width, height);
            Backward(distance, width, height);
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

        private static void Forward(float[] d, int width, int height)
        {
            for (var z = 0; z < height; z++)
            {
                for (var x = 0; x < width; x++)
                {
                    var i = z * width + x;
                    if (x > 0) d[i] = Mathf.Min(d[i], d[i - 1] + Straight);
                    if (z > 0) d[i] = Mathf.Min(d[i], d[i - width] + Straight);
                    if (x > 0 && z > 0) d[i] = Mathf.Min(d[i], d[i - width - 1] + Diagonal);
                    if (x < width - 1 && z > 0) d[i] = Mathf.Min(d[i], d[i - width + 1] + Diagonal);
                }
            }
        }

        private static void Backward(float[] d, int width, int height)
        {
            for (var z = height - 1; z >= 0; z--)
            {
                for (var x = width - 1; x >= 0; x--)
                {
                    var i = z * width + x;
                    if (x < width - 1) d[i] = Mathf.Min(d[i], d[i + 1] + Straight);
                    if (z < height - 1) d[i] = Mathf.Min(d[i], d[i + width] + Straight);
                    if (x < width - 1 && z < height - 1) d[i] = Mathf.Min(d[i], d[i + width + 1] + Diagonal);
                    if (x > 0 && z < height - 1) d[i] = Mathf.Min(d[i], d[i + width - 1] + Diagonal);
                }
            }
        }
    }
}
