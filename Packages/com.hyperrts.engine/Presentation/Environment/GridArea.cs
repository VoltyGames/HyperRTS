using HyperRTS.Simulation.Common;
using Unity.Mathematics;

namespace HyperRTS.Presentation.Environment
{
    /// <summary>A rectangle of square cells over the map: which cell a position is in, where a cell is, and its index.</summary>
    public readonly struct GridArea
    {
        public readonly float2 Min;
        public readonly float CellSize;
        public readonly int2 Size;

        public GridArea(float2 min, float cellSize, int2 size)
        {
            Min = min;
            CellSize = cellSize;
            Size = size;
        }

        /// <summary>The cell under <paramref name="position"/>, which may lie off the grid.</summary>
        public int2 Cell(float2 position) => (int2)math.floor((position - Min) / CellSize);

        /// <summary>The cell at <paramref name="index"/>, counted along x then z.</summary>
        public int2 FromIndex(int index) => new(index % Size.x, index / Size.x);

        public float2 Centre(int2 cell) => Min + ((float2)cell + 0.5f) * CellSize;

        public bool Inside(int2 cell) => math.all(cell >= 0) && math.all(cell < Size);

        public int Index(int2 cell) => cell.x + cell.y * Size.x;
    }
}
