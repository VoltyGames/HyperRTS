using Unity.Entities;
using Unity.Netcode;

namespace HyperRTS.Simulation.Resources
{
    /// <summary>Remaining amount of a harvestable deposit and its regrowth.</summary>
    public struct ResourceNode : IComponentData
    {
        public UnityObjectRef<ResourceType> Type;
        [GhostField] public int Amount;
        [GhostField] public int MaxAmount;
        public float RegrowthPerSecond;

        /// <summary>Fractional regrowth carried between frames.</summary>
        public float RegrowthAccumulator;

        /// <summary>The share of the starting amount still left, 0 to 1.</summary>
        public readonly float Remaining => (float)Amount / MaxAmount;
    }
}
