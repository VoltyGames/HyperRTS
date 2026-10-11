using HyperRTS.Core;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Resources;
using HyperRTS.Simulation.Selection;
using HyperRTS.Simulation.Transport;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace HyperRTS.Simulation.Production
{
    /// <summary>
    /// Clears what the old owner left on an entity that changed hands this frame (<see cref="OwnerChanged"/>): its
    /// production queue is refunded to the old owner, it stops every order, movement and attack, and its rally point
    /// and selection are dropped.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(LifecycleSystemGroup))]
    [UpdateAfter(typeof(NeutralGarrisonSystem))]
    public partial struct OwnerChangeSystem : ISystem
    {
        private BufferLookup<ProductionQueueItem> _queues;
        private BufferLookup<ResourceStock> _stocks;
        private BufferLookup<ResourceCost> _costs;
        private OrderWriter _orders;
        private ComponentLookup<RallyPoint> _rallies;
        private ComponentLookup<Selected> _selected;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerRegistry>();
            _queues = state.GetBufferLookup<ProductionQueueItem>(true);
            _stocks = state.GetBufferLookup<ResourceStock>();
            _costs = state.GetBufferLookup<ResourceCost>(true);
            _orders = new OrderWriter(ref state);
            _rallies = state.GetComponentLookup<RallyPoint>(true);
            _selected = state.GetComponentLookup<Selected>(true);
            state.RequireForUpdate<GameEvents>();
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _queues.Update(ref state);
            _stocks.Update(ref state);
            _costs.Update(ref state);
            _orders.Update(ref state);
            _rallies.Update(ref state);
            _selected.Update(ref state);

            new ClearJob
            {
                Ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                    .CreateCommandBuffer(state.WorldUnmanaged),
                Players = SystemAPI.GetSingleton<PlayerRegistry>(),
                Queues = _queues,
                Stocks = _stocks,
                Costs = _costs,
                Orders = _orders,
                Rallies = _rallies,
                Selected = _selected,
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
