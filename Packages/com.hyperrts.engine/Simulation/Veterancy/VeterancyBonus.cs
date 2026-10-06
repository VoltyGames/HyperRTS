using HyperRTS.Simulation.Stats;
using Unity.Entities;

namespace HyperRTS.Simulation.Veterancy
{
    /// <summary>A modifier active from <see cref="Rank"/> upward.</summary>
    [InternalBufferCapacity(0)]
    public struct VeterancyBonus : IBufferElementData
    {
        public byte Rank;
        public StatModifier Modifier;
    }
}
