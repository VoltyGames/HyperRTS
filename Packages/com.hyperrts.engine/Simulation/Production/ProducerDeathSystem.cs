using HyperRTS.Simulation.Common.Combat;
using HyperRTS.Simulation.Common.Players;
using HyperRTS.Simulation.Common.Systems;
using HyperRTS.Simulation.Resources;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace HyperRTS.Simulation.Production
{
    /// <summary>
    /// Refunds the queue of a destroyed producer to its owner, as selling or capturing it does, on the frame of the
    /// killing blow; the emptied queue never refunds twice.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(LifecycleSystemGroup))]
    public partial struct ProducerDeathSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerRegistry>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            new RefundJob
            {
                Players = SystemAPI.GetSingleton<PlayerRegistry>(),
                Stocks = SystemAPI.GetBufferLookup<ResourceStock>(false),
                Costs = SystemAPI.GetBufferLookup<ResourceCost>(true),
            }.Schedule();
        }

        /// <summary>Single-threaded: producers of one owner share its stockpile.</summary>
        [BurstCompile]
        [WithAll(typeof(Producer))]
        [WithChangeFilter(typeof(Health))]
        private partial struct RefundJob : IJobEntity
        {
            public PlayerRegistry Players;
            public BufferLookup<ResourceStock> Stocks;
            [ReadOnly] public BufferLookup<ResourceCost> Costs;

            private void Execute(in Health health, in Owner owner, DynamicBuffer<ProductionQueueItem> queue)
            {
                var alive = health.Current > 0f;
                if (alive || queue.IsEmpty)
                {
                    return;
                }

                new ProductionQueue(queue).RefundAll(new Stockpile(Stocks[Players[owner.Value]]), Costs);
            }
        }
    }
}
