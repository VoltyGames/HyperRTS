using Unity.Collections;
using Unity.Entities;

namespace HyperRTS.Simulation.Production
{
    /// <summary>What one player's purchases are checked against, snapshotted once for many checks.</summary>
    public struct Buyer
    {
        public byte Faction;
        public CompletedBuildings Buildings;
        public DynamicBuffer<CompletedResearch> Research;

        /// <summary>Research the player has queued anywhere; only read when the prefab is research.</summary>
        public NativeHashSet<Entity> QueuedResearch;
    }
}
