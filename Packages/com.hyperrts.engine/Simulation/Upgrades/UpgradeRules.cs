using HyperRTS.Simulation.Buildings;
using Unity.Entities;

namespace HyperRTS.Simulation.Upgrades
{
    /// <summary>Whether a player may still queue an upgrade: each is researched once.</summary>
    public static class UpgradeRules
    {
        public static bool IsResearched(DynamicBuffer<ResearchedUpgrade> researched, Entity upgrade)
        {
            foreach (var item in researched)
            {
                if (item.Upgrade == upgrade)
                {
                    return true;
                }
            }

            return false;
        }

        public static bool IsQueued(DynamicBuffer<ProductionQueueItem> queue, Entity upgrade)
        {
            foreach (var item in queue)
            {
                if (item.Prefab == upgrade)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
