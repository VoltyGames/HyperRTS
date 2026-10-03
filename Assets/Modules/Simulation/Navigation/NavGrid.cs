using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Navigation
{
    /// <summary>Singleton walkability grid over the map; a cell is blocked when non-zero.</summary>
    public struct NavGrid : IComponentData
    {
        public NativeArray<byte> Cells;
        public int2 Size;
        public float2 Min;
        public float CellSize;

        /// <summary>Bumped on every rebuild so paths can be revalidated.</summary>
        public int Version;

        public readonly bool IsCreated => Cells.IsCreated;

        public readonly int2 WorldToCell(float3 position) => WorldToCell(position.xz);

        public readonly int2 WorldToCell(float2 position) => (int2)math.floor((position - Min) / CellSize);

        public readonly float3 CellCenter(int2 cell) =>
            new(Min.x + (cell.x + 0.5f) * CellSize, 0f, Min.y + (cell.y + 0.5f) * CellSize);

        public readonly bool InBounds(int2 cell) => math.all(cell >= 0 & cell < Size);

        public readonly int Index(int2 cell) => cell.y * Size.x + cell.x;

        public readonly bool IsWalkable(int2 cell) => InBounds(cell) && Cells[Index(cell)] == 0;

        public readonly bool IsWalkable(float3 position) => IsWalkable(WorldToCell(position));

        /// <summary>True when every cell under an XZ box is inside the map and unblocked.</summary>
        public readonly bool IsAreaFree(float3 center, float2 size)
        {
            var half = new float3(size.x * 0.5f, 0f, size.y * 0.5f);
            var min = WorldToCell(center - half);
            var max = WorldToCell(center + half - 0.001f);

            for (var y = min.y; y <= max.y; y++)
            {
                for (var x = min.x; x <= max.x; x++)
                {
                    if (!IsWalkable(new int2(x, y)))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        /// <summary>Closest walkable cell by ring distance, searching up to <paramref name="maxRadius"/> rings out.</summary>
        public readonly bool TryFindNearestWalkable(int2 cell, int maxRadius, out int2 found)
        {
            cell = math.clamp(cell, 0, Size - 1);
            found = cell;
            if (IsWalkable(cell))
            {
                return true;
            }

            for (var r = 1; r <= maxRadius; r++)
            {
                var best = int.MaxValue;
                for (var i = -r; i <= r; i++)
                {
                    Consider(cell, new int2(i, -r), ref best, ref found);
                    Consider(cell, new int2(i, r), ref best, ref found);
                    Consider(cell, new int2(-r, i), ref best, ref found);
                    Consider(cell, new int2(r, i), ref best, ref found);
                }

                if (best != int.MaxValue)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// True when a straight walk stays on walkable cells without cutting blocked corners. A non-zero
        /// clearance also tests two parallel lines that far to each side, so smoothed paths keep off walls.
        /// </summary>
        public readonly bool HasLineOfSight(float3 from, float3 to, float clearance)
        {
            var delta = to.xz - from.xz;
            var length = math.length(delta);
            if (clearance <= 0f || length < 1e-4f)
            {
                return IsSegmentClear(from.xz, to.xz);
            }

            var side = new float2(-delta.y, delta.x) / length * clearance;
            return IsSegmentClear(from.xz, to.xz) &&
                   IsSegmentClear(from.xz + side, to.xz + side) &&
                   IsSegmentClear(from.xz - side, to.xz - side);
        }

        private readonly void Consider(int2 center, int2 offset, ref int best, ref int2 found)
        {
            var distance = offset.x * offset.x + offset.y * offset.y;
            if (distance < best && IsWalkable(center + offset))
            {
                best = distance;
                found = center + offset;
            }
        }

        private readonly bool IsSegmentClear(float2 from, float2 to)
        {
            // Quarter-cell samples are fine enough at unit scale and far simpler than an exact grid walk.
            var steps = math.max(1, (int)math.ceil(math.distance(from, to) / (CellSize * 0.25f)));
            var previous = WorldToCell(from);
            if (!IsWalkable(previous))
            {
                return false;
            }

            for (var i = 1; i <= steps; i++)
            {
                var cell = WorldToCell(math.lerp(from, to, i / (float)steps));
                if (cell.Equals(previous))
                {
                    continue;
                }

                var diagonal = cell.x != previous.x && cell.y != previous.y;
                if (!IsWalkable(cell) || (diagonal && (!IsWalkable(new int2(previous.x, cell.y)) ||
                                                       !IsWalkable(new int2(cell.x, previous.y)))))
                {
                    return false;
                }

                previous = cell;
            }

            return true;
        }
    }
}
