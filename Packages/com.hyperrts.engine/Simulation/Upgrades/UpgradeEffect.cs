using HyperRTS.Simulation.Stats;
using Unity.Entities;

namespace HyperRTS.Simulation.Upgrades
{
    /// <summary>One modifier an upgrade grants; <see cref="AppliesTo"/> is an entity type id, 0 for every type.</summary>
    [InternalBufferCapacity(2)]
    public struct UpgradeEffect : IBufferElementData
    {
        public int AppliesTo;
        public StatModifier Modifier;
    }
}
