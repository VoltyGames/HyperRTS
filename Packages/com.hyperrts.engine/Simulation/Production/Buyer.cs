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

        /// <summary>How many of each build-limited type the player has; add to it after each purchase.</summary>
        public BuildCounts Counts;

        /// <summary>
        /// Snapshots <paramref name="faction"/> as a buyer for this frame, collecting what it has queued at every
        /// producer in <paramref name="queues"/> (<see cref="ResearchRules.QueueQuery"/>) and its build-limited
        /// entities in <paramref name="limited"/> (<see cref="BuildCounts.Query"/>).
        /// </summary>
        public static Buyer Snapshot(EntityManager entityManager, EntityQuery queues, EntityQuery limited,
            byte faction, CompletedBuildings buildings, DynamicBuffer<CompletedResearch> research)
        {
            var queued = new NativeHashSet<Entity>(8, Allocator.Temp);
            ResearchRules.CollectQueued(entityManager, queues, faction, queued);
            return new Buyer
            {
                Faction = faction,
                Buildings = buildings,
                Research = research,
                QueuedResearch = queued,
                Counts = BuildCounts.Snapshot(entityManager, limited, queues, faction),
            };
        }
    }
}
