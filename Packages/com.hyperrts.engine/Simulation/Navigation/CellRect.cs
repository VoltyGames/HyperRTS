using Unity.Mathematics;

namespace HyperRTS.Simulation.Navigation
{
    /// <summary>A box of grid cells from <see cref="First"/>, <see cref="Cells"/> across, indexed row by row.</summary>
    internal readonly struct CellRect
    {
        public readonly int2 First;
        public readonly int2 Cells;

        public CellRect(int2 first, int2 cells)
        {
            First = first;
            Cells = cells;
        }

        public int Count => Cells.x * Cells.y;

        /// <summary>The cell's index in the box, or -1 outside it.</summary>
        public int Index(int2 cell)
        {
            var local = cell - First;
            return math.any(local < 0 | local >= Cells) ? -1 : local.y * Cells.x + local.x;
        }

        public int2 Cell(int index) => First + new int2(index % Cells.x, index / Cells.x);
    }
}
