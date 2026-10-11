using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Navigation;
using Unity.Collections;
using Unity.Entities;

namespace HyperRTS.Simulation.Orders
{
    /// <summary>
    /// Stops what the previous order left running (movement, an attack, move bookkeeping) before a new order starts.
    /// Parallel jobs may use it on the entity they are processing only.
    /// </summary>
    public struct OrderHalt
    {
        [NativeDisableParallelForRestriction] private ComponentLookup<MoveDestination> _move;
        [NativeDisableParallelForRestriction] private ComponentLookup<AttackTarget> _attack;
        [NativeDisableParallelForRestriction] private ComponentLookup<MoveOrderState> _moveOrder;

        public OrderHalt(ref SystemState state)
        {
            _move = state.GetComponentLookup<MoveDestination>();
            _attack = state.GetComponentLookup<AttackTarget>();
            _moveOrder = state.GetComponentLookup<MoveOrderState>();
        }

        public void Update(ref SystemState state)
        {
            _move.Update(ref state);
            _attack.Update(ref state);
            _moveOrder.Update(ref state);
        }

        public void Apply(Entity unit)
        {
            if (_move.HasComponent(unit))
            {
                _move.SetComponentEnabled(unit, false);
            }

            if (_attack.HasComponent(unit))
            {
                _attack.SetComponentEnabled(unit, false);
            }

            if (_moveOrder.HasComponent(unit))
            {
                _moveOrder.SetComponentEnabled(unit, false);
            }
        }
    }
}
