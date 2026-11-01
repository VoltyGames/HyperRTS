using HyperRTS.Simulation.Common;
using Unity.Entities;

namespace HyperRTS.Simulation.Navigation.Heights
{
    /// <summary>Immutable grid of ground heights: row-major samples at the corners, <see cref="GridFrame.Corner"/>.</summary>
    public struct HeightfieldBlob
    {
        public BlobArray<float> Heights;

        /// <summary>Counts samples, not the cells between them; its cell size is the sample spacing.</summary>
        public GridFrame Frame;
    }
}
