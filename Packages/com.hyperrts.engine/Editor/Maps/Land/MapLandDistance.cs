using System;
using HyperRTS.Editor.Maps.Geometry;
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
        public static MapValues<float> To(MapGrid grid, Func<int, int, bool> seed)
        {
            var field = new MapValues<float>(grid);
            var distance = field.Values;
            for (var z = 0; z < grid.Height; z++)
            {
                for (var x = 0; x < grid.Width; x++)
                {
                    distance[grid.Index(x, z)] = seed(x, z) ? 0f : float.MaxValue;
                }
            }

            Spread(grid, (cell, from, step) => distance[cell] = Mathf.Min(distance[cell], distance[from] + step));
            return field;
        }

        /// <summary>Distance from every cell to the nearest cell of another region (0 on a region's edge).</summary>
        public static MapValues<float> ToRegionEdge<T>(MapValues<T> region) where T : unmanaged, IEquatable<T> =>
            To(region.Grid, (x, z) => IsEdge(region, x, z));

        private static bool IsEdge<T>(MapValues<T> region, int x, int z) where T : unmanaged, IEquatable<T>
        {
            var grid = region.Grid;
            var own = region[x, z];
            if (x > 0 && !region[x - 1, z].Equals(own))
            {
                return true;
            }

            if (x < grid.Width - 1 && !region[x + 1, z].Equals(own))
            {
                return true;
            }

            if (z > 0 && !region[x, z - 1].Equals(own))
            {
                return true;
            }

            return z < grid.Height - 1 && !region[x, z + 1].Equals(own);
        }

        /// <summary>
        /// Spreads distances over a row-major grid, from the south-west corner and back from the north-east:
        /// <paramref name="carry"/> gets each cell, a neighbour this pass has already reached and the step between
        /// them (1 straight, 1.4142 diagonal), to carry that neighbour's distance over.
        /// </summary>
        public static void Spread(MapGrid grid, Action<int, int, float> carry)
        {
            var (width, height) = (grid.Width, grid.Height);
            for (var z = 0; z < height; z++)
            {
                for (var x = 0; x < width; x++)
                {
                    var i = grid.Index(x, z);
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
                    var i = grid.Index(x, z);
                    if (x < width - 1) carry(i, i + 1, Straight);
                    if (z < height - 1) carry(i, i + width, Straight);
                    if (x < width - 1 && z < height - 1) carry(i, i + width + 1, Diagonal);
                    if (x > 0 && z < height - 1) carry(i, i + width - 1, Diagonal);
                }
            }
        }
    }
}
