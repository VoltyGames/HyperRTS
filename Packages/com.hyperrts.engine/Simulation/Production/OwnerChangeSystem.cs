using HyperRTS.Core;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Resources;
using HyperRTS.Simulation.Selection;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace HyperRTS.Simulation.Production
{
    /// <summary>
    /// Clears what the old owner left on an entity that changed hands this frame (<see cref="OwnerChanged"/>): its
    /// production queue is refunded to the old owner, and its target, rally point and selection are dropped.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(LifecycleSystemGroup))]
    public partial struct OwnerChangeSystem : ISystem
    {
        private EntityQuery _players;
        private BufferLookup<ProductionQueueItem> _queues;
        private BufferLookup<ResourceStock> _stocks;
        private BufferLookup<ResourceCost> _costs;
        private ComponentLookup<AttackTarget> _attacks;
        private ComponentLookup<RallyPoint> _rallies;
        private ComponentLookup<Selected> _selected;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _players = SystemAPI.QueryBuilder().WithAll<Player>().Build();
            _queues = state.GetBufferLookup<ProductionQueueItem>(true);
            _stocks = state.GetBufferLookup<ResourceStock>();
            _costs = state.GetBufferLookup<ResourceCost>(true);
            _attacks = state.GetComponentLookup<AttackTarget>(true);
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
            _attacks.Update(ref state);
            _rallies.Update(ref state);
            _selected.Update(ref state);

            new ClearJob
            {
                Ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                    .CreateCommandBuffer(state.WorldUnmanaged),
                PlayerByFaction = PlayerLookup.ByFaction(_players, state.WorldUpdateAllocator),
                Queues = _queues,
                Stocks = _stocks,
                Costs = _costs,
                Attacks = _attacks,
                Rallies = _rallies,
                Selected = _selected,
            }.Schedule();
        }

        [BurstCompile]
        [WithAll(typeof(GameEvents))]
        private partial struct ClearJob : IJobEntity
        {
            public EntityCommandBuffer Ecb;
            [ReadOnly] public NativeArray<Entity> PlayerByFaction;
            [ReadOnly] public BufferLookup<ProductionQueueItem> Queues;
            public BufferLookup<ResourceStock> Stocks;
            [ReadOnly] public BufferLookup<ResourceCost> Costs;
            [ReadOnly] public ComponentLookup<AttackTarget> Attacks;
            [ReadOnly] public ComponentLookup<RallyPoint> Rallies;
            [ReadOnly] public ComponentLookup<Selected> Selected;

            private void Execute(in DynamicBuffer<OwnerChanged> changes)
            {
                foreach (var change in changes)
                {
                    RefundQueue(change.Entity, change.From);
                    Disable(Attacks, change.Entity);
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

                if (Stocks.TryGetBuffer(PlayerByFaction[owner], out var stock))
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
