using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Navigation.Agents;
using Unity.Entities;

namespace HyperRTS.Simulation.Orders
{
    /// <summary>
    /// Stops what the previous order left running (movement, arrival, attack, move bookkeeping) before a new order
    /// starts. Holds writable lookups, so use it from one thread.
    /// </summary>
    public struct OrderHalt
    {
        private ComponentLookup<MoveDestination> _move;
        private ComponentLookup<Arrival> _arrival;
        private ComponentLookup<AttackTarget> _attack;
        private ComponentLookup<MoveOrderState> _moveOrder;

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
