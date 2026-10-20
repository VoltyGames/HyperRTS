using System;
using Unity.Entities;

namespace HyperRTS.Core
{
    /// <summary>Pauses and restores the phase groups that play the game, so a new phase is paused everywhere at once.</summary>
    public static class GameplayPhases
    {
        // Every group in SystemGroups but replay playback's own. Orders first, so the join hold can keep them.
        private static readonly Type[] Groups =
        {
            typeof(OrderSystemGroup), typeof(AISystemGroup), typeof(MovementSystemGroup), typeof(CombatSystemGroup),
            typeof(ProductionSystemGroup), typeof(LifecycleSystemGroup),
        };

        /// <summary>Turns every phase off; returns which were on, for <see cref="Restore"/>.</summary>
        public static ulong PauseAll(World world) => Pause(world, 0);

        /// <summary>
        /// Turns every phase but the order phase off, so players still spawn and join while nobody, AI included,
        /// plays. Returns which were on, for <see cref="Restore"/>.
        /// </summary>
        public static ulong PauseAllButOrders(World world) => Pause(world, 1);

        /// <summary>Sets every phase back to what a pause returned.</summary>
        public static void Restore(World world, ulong enabled)
        {
            for (var i = 0; i < Groups.Length; i++)
            {
                Group(world, i).Enabled = (enabled & (1ul << i)) != 0;
            }
        }

        private static ulong Pause(World world, int firstPaused)
        {
            var enabled = 0ul;
            for (var i = 0; i < Groups.Length; i++)
            {
                var group = Group(world, i);
                enabled |= group.Enabled ? 1ul << i : 0ul;
                group.Enabled &= i < firstPaused;
            }

            return enabled;
        }

        private static ComponentSystemBase Group(World world, int index) =>
            world.GetExistingSystemManaged(Groups[index])
            ?? throw new InvalidOperationException($"World {world.Name} has no {Groups[index].Name}.");
    }
}
