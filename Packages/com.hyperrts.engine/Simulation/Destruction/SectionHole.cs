using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Destruction
{
    /// <summary>A holed variant of a <see cref="Section"/>'s wall or roof, and where its hole is, in the building's space.</summary>
    [InternalBufferCapacity(0)]
    public struct SectionHole : IBufferElementData
    {
        public byte Section;
        public Entity Model;
        public float3 Center;
    }
}
