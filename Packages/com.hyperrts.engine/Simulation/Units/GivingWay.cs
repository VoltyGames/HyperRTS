using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Orders;
using Unity.Collections;
using Unity.Entities;

namespace HyperRTS.Simulation.Units
{
    /// <summary>
    /// Decides which parked units step aside for a moving unit of their own side. Read-only, so parallel jobs can
    /// share it.
    /// </summary>
    public struct GivingWay
    {
        [ReadOnly] private ComponentLookup<ActiveOrder> _orders;
        [ReadOnly] private ComponentLookup<AttackTarget> _attacks;
        [ReadOnly] private ComponentLookup<CombatStance> _stances;

        public GivingWay(ref SystemState state)
        {
            _orders = state.GetComponentLookup<ActiveOrder>(true);
            _attacks = state.GetComponentLookup<AttackTarget>(true);
            _stances = state.GetComponentLookup<CombatStance>(true);
        }

        public void Update(ref SystemState state)
        {
            _orders.Update(ref state);
            _attacks.Update(ref state);
            _stances.Update(ref state);
        }

        /// <summary>
        /// Whether the parked unit steps aside for a mover of <paramref name="moverFaction"/>: only an idle ally does,
        /// as an order, a target or Hold Position holds its ground. Flight holds a landed aircraft itself.
        /// </summary>
        public readonly bool Yields(Entity parked, byte parkedFaction, byte moverFaction)
        {
            if (parkedFaction != moverFaction)
            {
                return false;
            }

            if (_orders.HasEnabled(parked) || _attacks.HasEnabled(parked))
            {
                return false;
            }

            return !_stances.TryGetComponent(parked, out var stance) || stance.Value != Stance.HoldPosition;
        }
    }
}
