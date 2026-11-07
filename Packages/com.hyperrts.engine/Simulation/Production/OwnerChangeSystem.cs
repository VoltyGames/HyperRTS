using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Events;
using HyperRTS.Simulation.Common.Players;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Resources;
using HyperRTS.Simulation.Selection;
using HyperRTS.Simulation.Transport.Garrison;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace HyperRTS.Simulation.Production
{
    /// <summary>
    /// Clears what the old owner left on an entity that changed hands (<see cref="OwnerChanged"/>): refunds its queue and drops its orders, rally point and selection.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(LifecycleSystemGroup))]
    [UpdateAfter(typeof(NeutralGarrisonSystem))]
    public partial struct OwnerChangeSystem : ISystem
    {
        private OrderWriter _orders;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerRegistry>();
            _orders = new OrderWriter(ref state);
            state.RequireForUpdate<GameEvents>();
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _orders.Update(ref state, SystemAPI.GetSingleton<OrderTraitTable>());

            new ClearJob
            {
                Ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                    .CreateCommandBuffer(state.WorldUnmanaged),
                Players = SystemAPI.GetSingleton<PlayerRegistry>(),
                Queues = SystemAPI.GetBufferLookup<ProductionQueueItem>(true),
                Stocks = SystemAPI.GetBufferLookup<ResourceStock>(false),
                Costs = SystemAPI.GetBufferLookup<ResourceCost>(true),
                Orders = _orders,
                Rallies = SystemAPI.GetComponentLookup<RallyPoint>(true),
                Selected = SystemAPI.GetComponentLookup<Selected>(true),
            }.Schedule();
        }

        /// <summary>Single-threaded: the order writer holds writable lookups.</summary>
        [BurstCompile]
        [WithAll(typeof(GameEvents))]
        private partial struct ClearJob : IJobEntity
        {
            public EntityCommandBuffer Ecb;
            public PlayerRegistry Players;
            [ReadOnly] public BufferLookup<ProductionQueueItem> Queues;
            public BufferLookup<ResourceStock> Stocks;
            [ReadOnly] public BufferLookup<ResourceCost> Costs;
            public OrderWriter Orders;
            [ReadOnly] public ComponentLookup<RallyPoint> Rallies;
            [ReadOnly] public ComponentLookup<Selected> Selected;

            private void Execute(in DynamicBuffer<OwnerChanged> changes)
            {
                foreach (var change in changes)
                {
                    RefundQueue(change.Entity, change.From);
                    Orders.Stop(change.Entity);
                    Disable(Rallies, change.Entity);
                    Disable(Selected, change.Entity);
                }
            }

            private void RefundQueue(Entity entity, byte owner)
            {
                if (!Queues.TryGetBuffer(entity, out var queue))
                {
                    return;
                }

                if (Stocks.TryGetBuffer(Players[owner], out var stock))
                {
                    ProductionRules.RefundQueue(stock, queue, Costs);
                }

                Ecb.SetBuffer<ProductionQueueItem>(entity);
            }

            private void Disable<T>(in ComponentLookup<T> lookup, Entity entity)
                where T : unmanaged, IComponentData, IEnableableComponent
            {
                if (lookup.HasComponent(entity))
                {
                    Ecb.SetComponentEnabled<T>(entity, false);
                }
            }
        }
    }
}
