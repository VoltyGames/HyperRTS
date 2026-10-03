using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Units;
using Unity.Entities;

namespace HyperRTS.Simulation.Orders
{
    /// <summary>Issues and cancels unit orders. Holds writable lookups, so use it from one thread.</summary>
    public struct OrderWriter
    {
        private ComponentLookup<ActiveOrder> _active;
        private BufferLookup<QueuedOrder> _queue;
        private ComponentLookup<MoveDestination> _move;
        private ComponentLookup<AttackTarget> _attack;

        public OrderWriter(ref SystemState state)
        {
            _active = state.GetComponentLookup<ActiveOrder>();
            _queue = state.GetBufferLookup<QueuedOrder>();
            _move = state.GetComponentLookup<MoveDestination>();
            _attack = state.GetComponentLookup<AttackTarget>();
        }

        public void Update(ref SystemState state)
        {
            _active.Update(ref state);
            _queue.Update(ref state);
            _move.Update(ref state);
            _attack.Update(ref state);
        }

        public bool CanReceiveOrders(Entity unit) => _active.HasComponent(unit);

        /// <summary>Replaces current orders, or appends when <paramref name="queue"/> and the unit is busy.</summary>
        public void Issue(Entity unit, in Order order, bool queue)
        {
            if (!_active.HasComponent(unit))
            {
                return;
            }

            var pending = _queue[unit];
            if (queue && (_active.IsComponentEnabled(unit) || pending.Length > 0))
            {
                pending.Add(new QueuedOrder { Value = order });
                return;
            }

            Interrupt(unit);
            _active[unit] = new ActiveOrder { Value = order };
            _active.SetComponentEnabled(unit, true);
        }

        /// <summary>Clears all orders and halts movement and attacks.</summary>
        public void Stop(Entity unit)
        {
            if (_active.HasComponent(unit))
            {
                Interrupt(unit);
            }
        }

        private void Interrupt(Entity unit)
        {
            _active.SetComponentEnabled(unit, false);
            _queue[unit].Clear();

            if (_move.HasComponent(unit))
            {
                _move.SetComponentEnabled(unit, false);
            }

            if (_attack.HasComponent(unit))
            {
                _attack.SetComponentEnabled(unit, false);
            }
        }
    }
}
