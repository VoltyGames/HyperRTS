using HyperRTS.Simulation.Navigation.Fields;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Navigation.Sectors
{
    /// <summary>How a nav grid divides into square sectors of <see cref="NavField.SectorCells"/> cells, row by row.</summary>
    public readonly struct SectorLayout
    {
        /// <summary>Sectors along each axis.</summary>
        public readonly int2 Count;

        public SectorLayout(int2 count) => Count = count;

        /// <summary>The layout of a grid of <paramref name="size"/> cells, its last sectors partly outside.</summary>
        public static SectorLayout Covering(int2 size) => new((size + NavField.SectorCells - 1) / NavField.SectorCells);

        public int Total => Count.x * Count.y;

        /// <summary>A sector's column and row.</summary>
        public int2 Coordinates(int sector) => new(sector % Count.x, sector / Count.x);

        /// <summary>The grid cell at a sector's low corner.</summary>
        public int2 Origin(int sector) => Coordinates(sector) * NavField.SectorCells;

        /// <summary>The sector holding a cell, or -1 off the sectors.</summary>
        public int Index(int2 cell)
        {
            var at = cell / NavField.SectorCells;
            var outside = math.any(cell < 0 | at >= Count);
            return outside ? -1 : at.y * Count.x + at.x;
        }

        /// <summary>The grid cell at a local index of a sector.</summary>
        public int2 Cell(int sector, int local) =>
            Origin(sector) + new int2(local % NavField.SectorCells, local / NavField.SectorCells);

        /// <summary>The columns and rows, both included, of the sectors a box of cells grown by <paramref name="halo"/> reaches.</summary>
        public void Range(int2 min, int2 max, int halo, out int2 low, out int2 high)
        {
            low = math.max((min - halo) / NavField.SectorCells, 0);
            high = math.min((max + halo) / NavField.SectorCells, Count - 1);
        }
    }
}
