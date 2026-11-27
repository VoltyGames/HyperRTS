using Unity.Entities;
using Unity.Transforms;

namespace HyperRTS.Simulation.Common.Systems;

// Frame order: Order (AI first), Movement, Combat (targeting, weapons, impacts, damage), Production, Lifecycle.
// GameplayPhaseGroup pauses them.
// Every phase exists on clients too, but systems default to the authoritative worlds (local and server);
// client-side ones (selection, input, fog view) opt in with their own [WorldSystemFilter].

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

/// <summary>Combat's first step: attack orders, target picks, chases, fire missions and deploy facing.</summary>
[WorldSystemFilter(SimulationWorlds.Authoritative, SimulationWorlds.Authoritative)]
[UpdateInGroup(typeof(CombatSystemGroup))]
public partial class TargetingSystemGroup : ComponentSystemGroup
{
}

/// <summary>Gives each weapon its target, turns it and fires it, after targeting decided who aims where.</summary>
[WorldSystemFilter(SimulationWorlds.Authoritative, SimulationWorlds.Authoritative)]
[UpdateInGroup(typeof(CombatSystemGroup))]
[UpdateAfter(typeof(TargetingSystemGroup))]
public partial class WeaponSystemGroup : ComponentSystemGroup
{
}

/// <summary>Flies projectiles and lands their impacts, after the weapons fired; damage applies last in combat.</summary>
[WorldSystemFilter(SimulationWorlds.Authoritative, SimulationWorlds.Authoritative)]
[UpdateInGroup(typeof(CombatSystemGroup))]
[UpdateAfter(typeof(WeaponSystemGroup))]
public partial class ImpactSystemGroup : ComponentSystemGroup
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
