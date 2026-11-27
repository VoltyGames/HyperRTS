using HyperRTS.Simulation.Common;
using Unity.Collections;
using Unity.Entities;

namespace HyperRTS.Simulation.Orders;

/// <summary>Reads a unit's orders without writing them, for presentation and placement.</summary>
public struct OrderReader
{
    [ReadOnly] private ComponentLookup<ActiveOrder> _active;
    [ReadOnly] private BufferLookup<QueuedOrder> _queue;
    [ReadOnly] private ComponentLookup<Inside> _inside;

    public OrderReader(ref SystemState state)
        : this(state.GetComponentLookup<ActiveOrder>(true), state.GetBufferLookup<QueuedOrder>(true),
            state.GetComponentLookup<Inside>(true))
    {
    }

    internal OrderReader(in ComponentLookup<ActiveOrder> active, in BufferLookup<QueuedOrder> queue,
        in ComponentLookup<Inside> inside)
    {
        _active = active;
        _queue = queue;
        _inside = inside;
    }

    public void Update(ref SystemState state)
    {
        _active.Update(ref state);
        _queue.Update(ref state);
        _inside.Update(ref state);
    }

    /// <summary>Passengers inside a container take no orders until they get out.</summary>
    public readonly bool CanReceiveOrders(Entity unit) => _active.HasComponent(unit) && !_inside.HasEnabled(unit);

    /// <inheritdoc cref="ActiveOrder.Current(in ComponentLookup{ActiveOrder}, Entity)"/>
    public readonly Order Current(Entity unit) => ActiveOrder.Current(_active, unit);

    /// <summary>The order the unit ends on: its last queued one, else the one it is executing.</summary>
    public readonly bool TryGetLastOrder(Entity unit, out Order order)
    {
        if (_queue.TryGetBuffer(unit, out var pending) && pending.Length > 0)
        {
            order = pending[^1].Value;
            return true;
        }

        order = Current(unit);
        return order.Type != OrderType.None;
    }
}
