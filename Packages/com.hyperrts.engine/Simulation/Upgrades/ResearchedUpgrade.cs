using Unity.Entities;

namespace HyperRTS.Simulation.Upgrades
{
    /// <summary>Upgrades a player has finished, in completion order; on the player entity.</summary>
    [InternalBufferCapacity(0)]
    public struct ResearchedUpgrade : IBufferElementData
    {
        /// <summary>The upgrade prefab, holding its <see cref="UpgradeEffect"/>s.</summary>
        public Entity Upgrade;
    }
}
