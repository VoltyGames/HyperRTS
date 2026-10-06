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
}
