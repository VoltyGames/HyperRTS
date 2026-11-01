using Unity.Mathematics;

namespace HyperRTS.Simulation.Common
{
    /// <summary>
    /// The layout of a grid of square cells over the map: <see cref="Size"/> cells from the south-west corner
    /// <see cref="Min"/>, each <see cref="CellSize"/> metres across, counted along x then z.
    /// </summary>
    public readonly struct CellGrid
    {
        public readonly float2 Min;
        public readonly float CellSize;
        public readonly int2 Size;

        public CellGrid(float2 min, float cellSize, int2 size)
        {
            Min = min;
            CellSize = cellSize;
            Size = size;
        }

        public int Count => Size.x * Size.y;

        /// <summary>The cell under a point, which may lie off the grid.</summary>
        public int2 Cell(float2 position) => (int2)math.floor((position - Min) / CellSize);

        public int2 Cell(float3 position) => Cell(position.xz);

        /// <summary>The cell at an index counted along x then z.</summary>
        public int2 Cell(int index) => new(index % Size.x, index / Size.x);

        public int Index(int2 cell) => cell.y * Size.x + cell.x;

        public bool Contains(int2 cell) => math.all(cell >= 0 & cell < Size);

        /// <summary>The nearest cell on the grid.</summary>
        public int2 Clamp(int2 cell) => math.clamp(cell, 0, Size - 1);

        public float2 Center(int2 cell) => Min + ((float2)cell + 0.5f) * CellSize;

        /// <summary>The cell's centre as a point at height 0, as paths and queries take it.</summary>
        public float3 CenterPoint(int2 cell)
        {
            var center = Center(cell);
            return new float3(center.x, 0f, center.y);
        }

        /// <summary>The cell's south-west corner, where corner-sampled grids such as heightfields keep each sample.</summary>
        public float2 Corner(int2 cell) => Min + (float2)cell * CellSize;

        /// <summary>A point in cells from <see cref="Min"/>: corners sit on whole numbers, centres half way between.</summary>
        public float2 Local(float2 position) => (position - Min) / CellSize;

        /// <summary>The cells from <paramref name="min"/> to <paramref name="max"/> that lie on the grid.</summary>
        public CellRange Cells(int2 min, int2 max) => new(math.max(min, 0), math.min(max, Size - 1));

        /// <summary>The cells on the grid whose centre lies within the flat circle.</summary>
        public CellCircle CellsInCircle(float2 center, float radius) =>
            new(this, Cells(Cell(center - radius), Cell(center + radius)), center, radius);
    }
}
