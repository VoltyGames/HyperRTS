using Unity.Entities;
using Unity.Transforms;

namespace HyperRTS.Core
{
    // Frame order: Order -> Movement -> Combat -> Production -> Lifecycle.
    // Every phase exists on clients too, but systems default to the authoritative worlds (local and server);
    // client-side ones (selection, input, fog view) opt in with their own [WorldSystemFilter].

    /// <summary>Selection and commands.</summary>
    [WorldSystemFilter(SimulationWorlds.All, SimulationWorlds.Authoritative)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(MovementSystemGroup))]
    public partial class OrderSystemGroup : ComponentSystemGroup
    {
    }

    /// <summary>Movement and pathfinding; before <see cref="TransformSystemGroup"/> so moves render this frame.</summary>
    [WorldSystemFilter(SimulationWorlds.All, SimulationWorlds.Authoritative)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(OrderSystemGroup))]
    [UpdateBefore(typeof(TransformSystemGroup))]
    public partial class MovementSystemGroup : ComponentSystemGroup
    {
    }

    /// <summary>Targeting and damage.</summary>
    [WorldSystemFilter(SimulationWorlds.All, SimulationWorlds.Authoritative)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(MovementSystemGroup))]
    public partial class CombatSystemGroup : ComponentSystemGroup
    {
    }

    /// <summary>Construction and production.</summary>
    [WorldSystemFilter(SimulationWorlds.All, SimulationWorlds.Authoritative)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(CombatSystemGroup))]
    public partial class ProductionSystemGroup : ComponentSystemGroup
    {
    }

    /// <summary>Death and cleanup; runs last so all damage lands first.</summary>
    [WorldSystemFilter(SimulationWorlds.All, SimulationWorlds.Authoritative)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(ProductionSystemGroup))]
    public partial class LifecycleSystemGroup : ComponentSystemGroup
    {
    }
}
