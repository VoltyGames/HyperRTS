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

        public readonly int2 WorldToCell(float3 position) => (int2)math.floor((position.xz - Min) / CellSize);

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
    }
}
