using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

namespace HyperRTS.Presentation.Environment.Grass
{
    /// <summary>One visible tile of one plant for Grass.compute to scatter (the layout the kernel declares).</summary>
    public struct GrassJob
    {
        public static readonly int Stride = UnsafeUtility.SizeOf<GrassJob>();

        /// <summary>World XZ of the tile's minimum corner.</summary>
        public Vector2 tileMin;

        /// <summary>Index into the field's plants and coverage slices.</summary>
        public int plant;

        /// <summary>The world-anchored lattice cells whose plant can fall in the tile.</summary>
        public Vector2Int first;
        public Vector2Int count;

        public GrassJob(Rect tile, int plant, float spacing)
        {
            tileMin = tile.min;
            this.plant = plant;
            first = new Vector2Int(Mathf.FloorToInt(tile.xMin / spacing), Mathf.FloorToInt(tile.yMin / spacing));
            var last = new Vector2Int(Mathf.FloorToInt(tile.xMax / spacing), Mathf.FloorToInt(tile.yMax / spacing));
            count = last - first + Vector2Int.one;
        }

        /// <summary>Kernel threads the job needs: one per cell.</summary>
        public int Cells => count.x * count.y;
    }
}
