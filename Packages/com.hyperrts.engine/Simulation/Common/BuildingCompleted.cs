using Unity.Entities;

namespace HyperRTS.Simulation.Common
{
    /// <summary>A building site was finished this frame.</summary>
    [InternalBufferCapacity(0)]
    public struct BuildingCompleted : IBufferElementData
    {
        public Entity Building;
        public byte Faction;
    }
}
