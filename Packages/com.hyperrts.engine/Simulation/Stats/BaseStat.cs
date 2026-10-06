using Unity.Entities;

namespace HyperRTS.Simulation.Stats
{
    /// <summary>A stat's unmodified value, captured from the live component the first time it is applied.</summary>
    [InternalBufferCapacity(0)]
    public struct BaseStat : IBufferElementData
    {
        public Stat Stat;
        public float Value;
    }
}
