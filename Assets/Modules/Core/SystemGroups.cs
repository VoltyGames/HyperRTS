using Unity.Entities;
using Unity.Transforms;

namespace HyperRTS.Core
{
    // Explicit simulation order. Systems pick a phase via
    // [UpdateInGroup(typeof(<Phase>SystemGroup))] instead of ordering ad hoc.
    // Frame order: Order -> Movement -> Combat -> Production -> Lifecycle.

    /// <summary>Input, selection and command resolution (roadmap phases 1–2).</summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(MovementSystemGroup))]
    public partial class OrderSystemGroup : ComponentSystemGroup
    {
    }

    /// <summary>Movement, pathfinding and steering (phase 3). Runs before
    /// <see cref="TransformSystemGroup"/> so moves render the same frame.</summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(OrderSystemGroup))]
    [UpdateBefore(typeof(TransformSystemGroup))]
    public partial class MovementSystemGroup : ComponentSystemGroup
    {
    }

    /// <summary>Target acquisition, attacks and damage (phase 7).</summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(MovementSystemGroup))]
    public partial class CombatSystemGroup : ComponentSystemGroup
    {
    }

    /// <summary>Construction and unit production (phase 5).</summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(CombatSystemGroup))]
    public partial class ProductionSystemGroup : ComponentSystemGroup
    {
    }

    /// <summary>Death and cleanup. Runs last so removals happen after all
    /// damage for the frame is applied.</summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(ProductionSystemGroup))]
    public partial class LifecycleSystemGroup : ComponentSystemGroup
    {
    }
}
