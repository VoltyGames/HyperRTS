using HyperRTS.Simulation.Common.Combat;
using HyperRTS.Simulation.Navigation.Agents;
using Unity.Collections;
using Unity.Entities;

namespace HyperRTS.Simulation.Orders;

/// <summary>
/// Stops what the previous order left running (movement, arrival, attack, move bookkeeping) before a new order
/// starts, for one unit or a unit in a chunk job. Writes the unit's own chunk, so a parallel job may use it.
/// </summary>
public struct OrderHalt
{
    [ReadOnly] private EntityStorageInfoLookup _storage;
    private ComponentTypeHandle<MoveDestination> _move;
    private ComponentTypeHandle<Arrival> _arrival;
    private ComponentTypeHandle<AttackTarget> _attack;
    private ComponentTypeHandle<MoveOrderState> _moveOrder;

    public OrderHalt(ref SystemState state)
    {
        _storage = state.GetEntityStorageInfoLookup();
        _move = state.GetComponentTypeHandle<MoveDestination>();
        _arrival = state.GetComponentTypeHandle<Arrival>();
        _attack = state.GetComponentTypeHandle<AttackTarget>();
        _moveOrder = state.GetComponentTypeHandle<MoveOrderState>();
    }

    public void Update(ref SystemState state)
    {
        _storage.Update(ref state);
        _move.Update(ref state);
        _arrival.Update(ref state);
        _attack.Update(ref state);
        _moveOrder.Update(ref state);
    }

    public void Apply(Entity unit)
    {
        // An entity destroyed earlier this frame has nothing left running.
        if (!_storage.Exists(unit))
        {
            return;
        }

        var place = _storage[unit];
        Apply(place.Chunk, place.IndexInChunk);
    }

    public void Apply(in ArchetypeChunk chunk, int index)
    {
        DisableIfPresent(chunk, index, ref _move);
        // So a walk-up never reads the last order's arrival as its own.
        DisableIfPresent(chunk, index, ref _arrival);
        DisableIfPresent(chunk, index, ref _attack);
        DisableIfPresent(chunk, index, ref _moveOrder);
    }

    private static void DisableIfPresent<T>(in ArchetypeChunk chunk, int index, ref ComponentTypeHandle<T> handle)
        where T : unmanaged, IComponentData, IEnableableComponent
    {
        if (chunk.Has(ref handle))
        {
            chunk.SetComponentEnabled(ref handle, index, false);
        }
    }
}
