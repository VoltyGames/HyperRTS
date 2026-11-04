using HyperRTS.Simulation.Common.Cells;
using Unity.Entities;

namespace HyperRTS.Simulation.Navigation.Heights
{
    /// <summary>Immutable grid of ground heights: row-major samples at the corners, <see cref="CellGrid.Corner"/>.</summary>
    public struct HeightfieldBlob
    {
        public BlobArray<float> Heights;

        /// <summary>Counts samples, not the cells between them; its cell size is the sample spacing.</summary>
        public CellGrid Grid;
    }
}
