using System;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Transforms;

namespace HyperRTS.Core
{
    // Frame order: Order (AI first), Movement, Combat, Production, Lifecycle. GameplayPhaseGroup pauses them.
    // Every phase exists on clients too, but systems default to the authoritative worlds (local and server);
    // client-side ones (selection, input, fog view) opt in with their own [WorldSystemFilter].

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

    /// <summary>Selection and commands.</summary>
    [WorldSystemFilter(SimulationWorlds.All, SimulationWorlds.Authoritative)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(MovementSystemGroup))]
    public partial class OrderSystemGroup : GameplayPhaseGroup
    {
    }

    /// <summary>Every computer player's systems, first in the order phase so their commands land this frame.</summary>
    [WorldSystemFilter(SimulationWorlds.Authoritative, SimulationWorlds.Authoritative)]
    [UpdateInGroup(typeof(OrderSystemGroup), OrderFirst = true)]
    public partial class AISystemGroup : GameplayPhaseGroup
    {
    }

    /// <summary>Movement and pathfinding; before <see cref="TransformSystemGroup"/> so moves render this frame.</summary>
    [WorldSystemFilter(SimulationWorlds.All, SimulationWorlds.Authoritative)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(OrderSystemGroup))]
    [UpdateBefore(typeof(TransformSystemGroup))]
    public partial class MovementSystemGroup : GameplayPhaseGroup
    {
    }

    /// <summary>Targeting and damage.</summary>
    [WorldSystemFilter(SimulationWorlds.All, SimulationWorlds.Authoritative)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(MovementSystemGroup))]
    public partial class CombatSystemGroup : GameplayPhaseGroup
    {
    }

    /// <summary>Construction and production.</summary>
    [WorldSystemFilter(SimulationWorlds.All, SimulationWorlds.Authoritative)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(CombatSystemGroup))]
    public partial class ProductionSystemGroup : GameplayPhaseGroup
    {
    }

    /// <summary>Death and cleanup; runs last so all damage lands first.</summary>
    [WorldSystemFilter(SimulationWorlds.All, SimulationWorlds.Authoritative)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(ProductionSystemGroup))]
    public partial class LifecycleSystemGroup : GameplayPhaseGroup
    {
    }

    /// <summary>Replay playback, local world only: drives entities while the gameplay phases are switched off.</summary>
    [WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation, WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(MovementSystemGroup))]
    [UpdateBefore(typeof(TransformSystemGroup))]
    public partial class ReplaySystemGroup : ComponentSystemGroup
    {
    }
}
