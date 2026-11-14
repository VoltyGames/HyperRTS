using HyperRTS.Simulation.Common.Systems;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;

namespace HyperRTS.Simulation.Orders
{
    /// <summary>Starts the next <see cref="QueuedOrder"/> on units whose <see cref="ActiveOrder"/> has finished.</summary>
    [BurstCompile]
    [UpdateInGroup(typeof(OrderSystemGroup))]
    public partial struct OrderDispatchSystem : ISystem
    {
        private EntityQuery _idle;
        private OrderHalt _halt;

        public void OnCreate(ref SystemState state)
        {
            _idle = new EntityQueryBuilder(Allocator.Temp).WithDisabledRW<ActiveOrder>().WithAllRW<QueuedOrder>()
                .Build(ref state);
            _halt = new OrderHalt(ref state);
            // Created here so every world that runs orders has it before any system updates.
            state.EntityManager.CreateSingleton(OrderTraitTable.Create(), "OrderTraitTable");
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _halt.Update(ref state);
            state.Dependency = new DispatchJob
            {
                Active = SystemAPI.GetComponentTypeHandle<ActiveOrder>(),
                Queues = SystemAPI.GetBufferTypeHandle<QueuedOrder>(),
                Halt = _halt,
            }.ScheduleParallel(_idle, state.Dependency);
        }

        // Writes only the dispatching unit's own components, so chunks with nothing queued keep their versions.
        [BurstCompile]
        private struct DispatchJob : IJobChunk
        {
            public ComponentTypeHandle<ActiveOrder> Active;
            public BufferTypeHandle<QueuedOrder> Queues;
            public OrderHalt Halt;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask,
                in v128 chunkEnabledMask)
            {
                var queues = chunk.GetBufferAccessorRO(ref Queues);
                var entities = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
                while (entities.NextEntityIndex(out var i))
                {
                    if (queues[i].Length == 0)
                    {
                        continue;
                    }

                    // Same reset as a fresh order, so a target picked up while idle can't hold the next order back.
                    Halt.Apply(chunk, i);
                    var active = chunk.GetNativeArray(ref Active);
                    active[i] = new ActiveOrder { Value = queues[i][0].Value };
                    chunk.GetBufferAccessorRW(ref Queues)[i].RemoveAt(0);
                    chunk.SetComponentEnabled(ref Active, i, true);
                }
            }
        }
    }
}
