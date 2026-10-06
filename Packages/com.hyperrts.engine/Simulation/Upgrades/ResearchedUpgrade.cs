using Unity.Entities;
using Unity.NetCode;

namespace HyperRTS.Simulation.Upgrades
{
    /// <summary>Upgrades a player has finished, in completion order; on the player entity.</summary>
    [InternalBufferCapacity(0)]
    public struct ResearchedUpgrade : IBufferElementData
    {
        /// <summary>The upgrade prefab, holding its <see cref="UpgradeEffect"/>s. Clients resolve it from TypeId.</summary>
        [GhostField(SendData = false)] public Entity Upgrade;

        [GhostField] public int TypeId;
    }
}
