using System;
using HyperRTS.Simulation.Common;
using Unity.Collections;
using Unity.Entities;

namespace HyperRTS.Simulation.Orders
{
    /// <summary>Issues and cancels unit orders. Holds writable lookups, so use it from one thread.</summary>
    public struct OrderWriter
    {
        private ComponentLookup<ActiveOrder> _active;
        private BufferLookup<QueuedOrder> _queue;
        private OrderHalt _halt;
        [ReadOnly] private ComponentLookup<Inside> _inside;
        private OrderTraitTable _traits;

        public OrderWriter(ref SystemState state)
        {
            _active = state.GetComponentLookup<ActiveOrder>();
            _queue = state.GetBufferLookup<QueuedOrder>();
            _halt = new OrderHalt(ref state);
            _inside = state.GetComponentLookup<Inside>(true);
            state.RequireForUpdate<OrderTraitTable>();
            _traits = default;
        }

        /// <summary>Pass <c>SystemAPI.GetSingleton&lt;OrderTraitTable&gt;()</c>: jobs copy the writer, so it can't hold a query.</summary>
        public void Update(ref SystemState state, in OrderTraitTable traits)
        {
            _active.Update(ref state);
            _queue.Update(ref state);
            _halt.Update(ref state);
            _inside.Update(ref state);
            _traits = traits;
        }

        /// <summary>The traits table, for callers that branch on how an order runs.</summary>
        public readonly OrderTraitTable Traits => _traits;

        /// <summary>Reads the orders this writer sees, including its own writes.</summary>
        public readonly OrderReader Reader => new(_active, _queue, _inside);

        /// <summary>Replaces current orders, or appends when <paramref name="queue"/> and the unit is busy.</summary>
        public void Issue(Entity unit, in Order order, bool queue)
        {
            if (!_traits.IsRegistered(order.Type))
            {
                throw new InvalidOperationException("HyperRTS: a game order type has no traits in OrderTraitTable.");
            }

            if (!Reader.CanReceiveOrders(unit))
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

        /// <summary>Clears all orders and halts movement and attacks; on an entity without orders (a tower), only halts.</summary>
        public void Stop(Entity unit)
        {
            if (_active.HasComponent(unit))
            {
                Interrupt(unit);
                return;
            }

            _halt.Apply(unit);
        }

        private void Interrupt(Entity unit)
        {
            _active.SetComponentEnabled(unit, false);
            _queue[unit].Clear();
            _halt.Apply(unit);
        }
    }
}
