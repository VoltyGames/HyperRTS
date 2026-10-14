using HyperRTS.Simulation.Common;
using Unity.Entities;

namespace HyperRTS.Simulation.Upgrades
{
    /// <summary>How many of <see cref="Faction"/>'s researched upgrades this entity already carries.</summary>
    public struct AppliedUpgrades : IComponentData
    {
        public int Count;

        /// <summary>The owner they came from; a new owner's upgrades replace them.</summary>
        public byte Faction;
    }

    /// <summary>One modifier an upgrade grants; <see cref="AppliesTo"/> is an entity type id, 0 for every type.</summary>
    [InternalBufferCapacity(2)]
    public struct UpgradeEffect : IBufferElementData
    {
        public int AppliesTo;
        public StatModifier Modifier;
    }
}
