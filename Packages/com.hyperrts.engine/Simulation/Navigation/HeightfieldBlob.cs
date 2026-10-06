using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Navigation
{
    /// <summary>Immutable grid of ground heights: row-major samples at <c>Min + (x, y) * Spacing</c>.</summary>
    public struct HeightfieldBlob
    {
        public BlobArray<float> Heights;
        public int2 Size;
        public float2 Min;
        public float Spacing;
    }
}
