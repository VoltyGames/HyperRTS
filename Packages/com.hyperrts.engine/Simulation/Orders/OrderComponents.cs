using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Navigation.Agents;
using HyperRTS.Simulation.Navigation.Reach;
using Unity.Entities;

namespace HyperRTS.Simulation.Orders;

/// <summary>The order a unit is executing; disabled means idle. The behaviour that finishes it disables it.</summary>
public struct ActiveOrder : IComponentData, IEnableableComponent
{
    public Order Value;

    /// <summary>The order the unit is carrying out, or one of type <see cref="OrderType.None"/> when idle.</summary>
    public static Order Current(in ComponentLookup<ActiveOrder> orders, Entity unit) =>
        orders.HasEnabled(unit) ? orders[unit].Value : default;

    /// <inheritdoc cref="Current(in ComponentLookup{ActiveOrder}, Entity)"/>
    public static Order Current(EntityManager entities, Entity unit) =>
        entities.HasEnabled<ActiveOrder>(unit) ? entities.GetComponentData<ActiveOrder>(unit).Value : default;

    /// <summary>Ends the order and halts the walk toward its target.</summary>
    public static void Finish(EnabledRefRW<ActiveOrder> busy, EnabledRefRW<MoveDestination> moving)
    {
        busy.ValueRW = false;
        moving.ValueRW = false;
    }

    /// <summary>Ends the order but leaves movement to whoever drives it, as combat chases an attacked target.</summary>
    public static void FinishLeavingMovement(EnabledRefRW<ActiveOrder> busy) => busy.ValueRW = false;

    /// <summary>True until the walk reaches its target; a stuck walk also finishes the order.</summary>
    public static bool StopsShort(ApproachState walk, EnabledRefRW<ActiveOrder> busy, EnabledRefRW<MoveDestination> moving)
    {
        if (walk == ApproachState.Stuck)
        {
            Finish(busy, moving);
        }

        return walk != ApproachState.Reached;
    }
}

/// <summary>Shift-queued orders, promoted to <see cref="ActiveOrder"/> one at a time.</summary>
[InternalBufferCapacity(4)]
public struct QueuedOrder : IBufferElementData
{
    public Order Value;
}

/// <summary>
/// Enabled once a move order has pointed locomotion at its goal; disabled whenever the order changes or combat
/// takes over, so the goal is re-applied afterwards.
/// </summary>
public struct MoveOrderState : IComponentData, IEnableableComponent
{
}
