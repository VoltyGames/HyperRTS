using Unity.Entities;

namespace HyperRTS.Simulation.Common
{
    /// <summary>A completed building of this <see cref="EntityInfo.TypeId"/> must be owned before producing.</summary>
    [InternalBufferCapacity(2)]
    public struct Prerequisite : IBufferElementData
    {
        public int TypeId;
    }
}
