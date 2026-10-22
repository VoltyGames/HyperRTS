using HyperRTS.Simulation.Air;
using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Orders;
using Unity.Collections;
using Unity.Entities;

namespace HyperRTS.Simulation.Units
{
    /// <summary>
    /// Which parked units step aside for a moving unit of their own side: idle ones. A unit with an order, a
    /// target, a pad or Hold Position holds its ground. Read-only, so parallel jobs can share it.
    /// </summary>
    public struct GivingWay
    {
        [ReadOnly] private ComponentLookup<ActiveOrder> _orders;
        [ReadOnly] private ComponentLookup<AttackTarget> _attacks;
        [ReadOnly] private ComponentLookup<CombatStance> _stances;
        [ReadOnly] private ComponentLookup<Docked> _docked;

        public GivingWay(ref SystemState state)
        {
            _orders = state.GetComponentLookup<ActiveOrder>(true);
            _attacks = state.GetComponentLookup<AttackTarget>(true);
            _stances = state.GetComponentLookup<CombatStance>(true);
            _docked = state.GetComponentLookup<Docked>(true);
        }

        public void Update(ref SystemState state)
        {
            _orders.Update(ref state);
            _attacks.Update(ref state);
            _stances.Update(ref state);
            _docked.Update(ref state);
        }

        public readonly bool IsLanded(Entity unit) => _docked.HasEnabled(unit);

        /// <summary>Whether the parked unit steps aside for a mover of <paramref name="moverFaction"/>.</summary>
        public readonly bool Yields(Entity parked, byte parkedFaction, byte moverFaction)
        {
            if (parkedFaction != moverFaction)
            {
                return false;
            }

            if (_orders.HasEnabled(parked) || _attacks.HasEnabled(parked) || IsLanded(parked))
            {
                return false;
            }

            return !_stances.TryGetComponent(parked, out var stance) || stance.Value != Stance.HoldPosition;
        }
    }
}
