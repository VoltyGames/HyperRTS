using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Stats;
using Unity.Entities;
using Unity.Mathematics;

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

        /// <summary><paramref name="amount"/> scaled by the player's <c>Stat.Income</c>, rounded.</summary>
        public static int Scaled(in BufferLookup<StatModifier> modifierLookup, Entity player, int amount)
        {
            var income = modifierLookup.TryGetBuffer(player, out var modifiers)
                ? StatMath.Evaluate(modifiers, Stat.Income, 1f)
                : 1f;
            return (int)math.round(amount * income);
        }
    }
}
