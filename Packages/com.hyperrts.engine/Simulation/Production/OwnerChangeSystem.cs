using HyperRTS.Simulation.Common.Events;
using HyperRTS.Simulation.Common.Players;
using HyperRTS.Simulation.Common.Systems;
using HyperRTS.Simulation.Resources;
using HyperRTS.Simulation.Transport.Garrison;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace HyperRTS.Simulation.Production
{
    /// <summary>
    /// Refunds the queue of a producer that changed hands (<see cref="OwnerChanged"/>) to its old owner and drops the
    /// old owner's rally point.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(LifecycleSystemGroup))]
    [UpdateAfter(typeof(NeutralGarrisonSystem))]
    public partial struct OwnerChangeSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerRegistry>();
            state.RequireForUpdate<GameEvents>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            new ClearJob
            {
                Players = SystemAPI.GetSingleton<PlayerRegistry>(),
                Queues = SystemAPI.GetBufferLookup<ProductionQueueItem>(false),
                Stocks = SystemAPI.GetBufferLookup<ResourceStock>(false),
                Costs = SystemAPI.GetBufferLookup<ResourceCost>(true),
                Rallies = SystemAPI.GetComponentLookup<RallyPoint>(false),
            }.Schedule();
        }

        /// <summary>Single-threaded: it writes the changed producers and the old owners' stockpiles.</summary>
        [BurstCompile]
        [WithAll(typeof(GameEvents))]
        private partial struct ClearJob : IJobEntity
        {
            public PlayerRegistry Players;
            public BufferLookup<ProductionQueueItem> Queues;
            public BufferLookup<ResourceStock> Stocks;
            [ReadOnly] public BufferLookup<ResourceCost> Costs;
            public ComponentLookup<RallyPoint> Rallies;

            private void Execute(in DynamicBuffer<OwnerChanged> changes)
            {
                foreach (var change in changes)
                {
                    RefundQueue(change.Entity, change.From);
                    if (Rallies.HasComponent(change.Entity))
                    {
                        Rallies.SetComponentEnabled(change.Entity, false);
                    }
                }
            }

            /// <summary>An empty queue is skipped: a neutral building has no stockpile to refund to.</summary>
            private void RefundQueue(Entity entity, byte owner)
            {
                if (!Queues.TryGetBuffer(entity, out var queue) || queue.IsEmpty)
                {
                    return;
                }

                new ProductionQueue(queue).RefundAll(new Stockpile(Stocks[Players[owner]]), Costs);
            }
        }
    }
}
