using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Navigation;
using Unity.Collections;
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
        private ComponentLookup<MoveOrderState> _moveOrder;
        [ReadOnly] private ComponentLookup<Inside> _inside;

        public OrderWriter(ref SystemState state)
        {
            _active = state.GetComponentLookup<ActiveOrder>();
            _queue = state.GetBufferLookup<QueuedOrder>();
            _move = state.GetComponentLookup<MoveDestination>();
            _attack = state.GetComponentLookup<AttackTarget>();
            _moveOrder = state.GetComponentLookup<MoveOrderState>();
            _inside = state.GetComponentLookup<Inside>(true);
        }

        public void Update(ref SystemState state)
        {
            _active.Update(ref state);
            _queue.Update(ref state);
            _move.Update(ref state);
            _attack.Update(ref state);
            _moveOrder.Update(ref state);
            _inside.Update(ref state);
        }

        /// <summary>Passengers inside a container take no orders until they get out.</summary>
        public bool CanReceiveOrders(Entity unit) => _active.HasComponent(unit) && !_inside.HasEnabled(unit);

        /// <summary>Whether the unit is carrying out an order of this type right now.</summary>
        public bool IsExecuting(Entity unit, OrderType type) =>
            _active.HasEnabled(unit) && _active[unit].Value.Type == type;

        /// <summary>The order the unit ends on: its last queued one, else the one it is executing.</summary>
        public bool TryGetLastOrder(Entity unit, out Order order)
        {
            order = default;
            if (_queue.TryGetBuffer(unit, out var pending) && pending.Length > 0)
            {
                order = pending[pending.Length - 1].Value;
                return true;
            }

            if (!_active.HasEnabled(unit))
            {
                return false;
            }

            order = _active[unit].Value;
            return true;
        }

        /// <summary>Replaces current orders, or appends when <paramref name="queue"/> and the unit is busy.</summary>
        public void Issue(Entity unit, in Order order, bool queue)
        {
            if (!CanReceiveOrders(unit))
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

            if (_moveOrder.HasComponent(unit))
            {
                _moveOrder.SetComponentEnabled(unit, false);
            }
        }
    }
}
