using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Navigation.Agents;
using Unity.Collections;
using Unity.Entities;

namespace HyperRTS.Simulation.Orders
{
    /// <summary>
    /// Stops what the previous order left running (movement, arrival, attack, move bookkeeping) before a new order
    /// starts. Parallel jobs may use it only on the entity they are processing.
    /// </summary>
    public struct OrderHalt
    {
        [NativeDisableParallelForRestriction] private ComponentLookup<MoveDestination> _move;
        [NativeDisableParallelForRestriction] private ComponentLookup<Arrival> _arrival;
        [NativeDisableParallelForRestriction] private ComponentLookup<AttackTarget> _attack;
        [NativeDisableParallelForRestriction] private ComponentLookup<MoveOrderState> _moveOrder;

        public OrderHalt(ref SystemState state)
        {
            _move = state.GetComponentLookup<MoveDestination>();
            _arrival = state.GetComponentLookup<Arrival>();
            _attack = state.GetComponentLookup<AttackTarget>();
            _moveOrder = state.GetComponentLookup<MoveOrderState>();
        }

        public void Update(ref SystemState state)
        {
            _move.Update(ref state);
            _arrival.Update(ref state);
            _attack.Update(ref state);
            _moveOrder.Update(ref state);
        }

        public void Apply(Entity unit)
        {
            _move.DisableIfPresent(unit);
            // So a walk-up never reads the last order's arrival as its own.
            _arrival.DisableIfPresent(unit);
            _attack.DisableIfPresent(unit);
            _moveOrder.DisableIfPresent(unit);
        }
    }
}
