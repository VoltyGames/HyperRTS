using HyperRTS.Simulation.Common;
using Unity.Entities;

namespace HyperRTS.Simulation.Resources
{
    /// <summary>Pays income into a player's stock and credits it as gathered.</summary>
    public static class ResourcePayout
    {
        public static void Pay(ref BufferLookup<ResourceStock> stockLookup, ref ComponentLookup<PlayerStats> statsLookup,
            Entity player, UnityObjectRef<ResourceType> type, int amount)
        {
            if (amount <= 0 || !stockLookup.TryGetBuffer(player, out var stock))
            {
                return;
            }

            ResourceMath.Add(stock, type, amount);
            if (statsLookup.TryGetRefRW(player, out var stats))
            {
                stats.ValueRW.ResourcesGathered += amount;
            }
        }
    }
}
