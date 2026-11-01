using System;
using System.Collections.Generic;
using Unity.Entities;

namespace HyperRTS.Core
{
    /// <summary>
    /// A phase group that plays the game. Pausing and restoring them all at once keeps a new phase paused
    /// everywhere; replay playback's own group is not one.
    /// </summary>
    public abstract partial class GameplayPhaseGroup : ComponentSystemGroup
    {
        /// <summary>Turns every phase off; returns which were on, for <see cref="Restore"/>.</summary>
        public static ulong PauseAll(World world) => Pause(world, null);

        /// <summary>
        /// Turns every phase but the order phase off, so players still spawn and join while nobody, AI included,
        /// plays. Returns which were on, for <see cref="Restore"/>.
        /// </summary>
        public static ulong PauseAllButOrders(World world) => Pause(world, typeof(OrderSystemGroup));

        /// <summary>Sets every phase back to what a pause returned.</summary>
        public static void Restore(World world, ulong enabled)
        {
            var bit = 0;
            foreach (var group in Phases(world))
            {
                group.Enabled = (enabled & (1ul << bit++)) != 0;
            }
        }

        private static ulong Pause(World world, Type kept)
        {
            var enabled = 0ul;
            var bit = 0;
            foreach (var group in Phases(world))
            {
                enabled |= group.Enabled ? 1ul << bit : 0ul;
                group.Enabled &= group.GetType() == kept;
                bit++;
            }

            return enabled;
        }

        // Creation order, so a pause and its restore agree on which bit is which group.
        private static IEnumerable<GameplayPhaseGroup> Phases(World world)
        {
            foreach (var system in world.Systems)
            {
                if (system is GameplayPhaseGroup group)
                {
                    yield return group;
                }
            }
        }
    }
}
