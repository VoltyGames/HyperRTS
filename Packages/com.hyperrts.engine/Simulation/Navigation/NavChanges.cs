using Unity.Collections;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Navigation
{
    /// <summary>
    /// The cells each recent <see cref="NavGrid.Version"/> changed, as one box per version, so a restamp re-plans only
    /// the paths that pass near it.
    /// </summary>
    public struct NavChanges
    {
        /// <summary>Versions remembered; a path planned further back re-plans.</summary>
        public const int History = 64;

        /// <summary>By version modulo <see cref="History"/>: the first and last changed cell, min above max for none.</summary>
        public NativeArray<int4> Areas;

        /// <summary>The cells as last stamped, kept by the restamp to compare with.</summary>
        public NativeArray<byte> Previous;

        /// <summary>First version with a recorded area: a rebuilt grid compares with nothing before it.</summary>
        public int Since;

        public static NavChanges Create(int cells, Allocator allocator) => new()
        {
            Areas = new NativeArray<int4>(History, allocator),
            Previous = new NativeArray<byte>(cells, allocator),
        };

        public void Dispose()
        {
            Areas.Dispose();
            Previous.Dispose();
        }

        /// <summary>Whether every version after <paramref name="planned"/> up to <paramref name="version"/> is recorded.</summary>
        public readonly bool Covers(int planned, int version) => planned + 1 >= Since && version - planned <= History;

        /// <summary>Records the box round every cell that differs from <see cref="Previous"/> as this version.</summary>
        public void Record(in NavGrid grid)
        {
            var min = grid.Size;
            var max = new int2(-1);
            for (var i = 0; i < grid.Cells.Length; i++)
            {
                if (Previous[i] != grid.Cells[i])
                {
                    var cell = grid.Cell(i);
                    min = math.min(min, cell);
                    max = math.max(max, cell);
                }
            }

            Areas[grid.Version % History] = new int4(min, max);
        }

        /// <summary>Whether a segment passes within <paramref name="margin"/> of cells changed after a version.</summary>
        public readonly bool Touches(in NavGrid grid, int planned, float2 from, float2 to, float margin)
        {
            for (var version = planned + 1; version <= grid.Version; version++)
            {
                var area = Areas[version % History];
                if (math.any(area.xy > area.zw))
                {
                    continue;
                }

                var low = grid.Min + (float2)area.xy * grid.CellSize;
                var high = grid.Min + (float2)(area.zw + 1) * grid.CellSize;
                if (NavCapsule.SegmentRectDistance(from, to, low, high) <= margin)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
