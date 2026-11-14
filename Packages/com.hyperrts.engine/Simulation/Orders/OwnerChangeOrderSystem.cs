using HyperRTS.Simulation.Common.Systems;
using HyperRTS.Simulation.Common.Events;
using Unity.Burst;
using Unity.Entities;

namespace HyperRTS.Simulation.Orders
{
    /// <summary>
    /// Stops the old owner's orders on an entity that changed hands (<see cref="OwnerChanged"/>). Last in the phase,
    /// after every system that hands entities over.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(LifecycleSystemGroup), OrderLast = true)]
    public partial struct OwnerChangeOrderSystem : ISystem
    {
        private OrderWriter _orders;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _orders = new OrderWriter(ref state);
            state.RequireForUpdate<GameEvents>();
            state.RequireForUpdate<OrderTraitTable>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _orders.Update(ref state, SystemAPI.GetSingleton<OrderTraitTable>());
            new StopJob { Orders = _orders }.Schedule();
        }

        /// <summary>Single-threaded: the order writer holds writable lookups.</summary>
        [BurstCompile]
        [WithAll(typeof(GameEvents))]
        private partial struct StopJob : IJobEntity
        {
            public OrderWriter Orders;

            private void Execute(in DynamicBuffer<OwnerChanged> changes)
            {
                foreach (var change in changes)
                {
                    Orders.Stop(change.Entity);
                }
            }
        }
    }
}
