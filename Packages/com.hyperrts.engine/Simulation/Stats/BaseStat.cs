using Unity.Entities;

namespace HyperRTS.Simulation.Stats
{
    /// <summary>A stat's unmodified value, recorded from the authored component when the entity spawns.</summary>
    [InternalBufferCapacity(0)]
    public struct BaseStat : IBufferElementData
    {
        public Stat Stat;
        public float Value;
    }
}
