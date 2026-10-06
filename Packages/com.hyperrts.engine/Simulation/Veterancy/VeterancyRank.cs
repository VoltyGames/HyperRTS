using Unity.Entities;

namespace HyperRTS.Simulation.Veterancy
{
    /// <summary>Experience threshold of rank index + 1, ascending.</summary>
    [InternalBufferCapacity(3)]
    public struct VeterancyRank : IBufferElementData
    {
        public float Experience;
    }
}
