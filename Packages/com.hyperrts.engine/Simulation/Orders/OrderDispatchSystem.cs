using HyperRTS.Simulation.Common.Combat;
using HyperRTS.Simulation.Common.Systems;
using HyperRTS.Simulation.Navigation.Agents;
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

        public void OnCreate(ref SystemState state)
        {
            _idle = new EntityQueryBuilder(Allocator.Temp).WithDisabledRW<ActiveOrder>().WithAllRW<QueuedOrder>()
                .Build(ref state);
            // Created here so every world that runs orders has it before any system updates.
            state.EntityManager.CreateSingleton(OrderTraitTable.Create(), "OrderTraitTable");
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            state.Dependency = new DispatchJob
            {
                Active = SystemAPI.GetComponentTypeHandle<ActiveOrder>(),
                Queues = SystemAPI.GetBufferTypeHandle<QueuedOrder>(),
                Move = SystemAPI.GetComponentTypeHandle<MoveDestination>(),
                Arrival = SystemAPI.GetComponentTypeHandle<Arrival>(),
                Attack = SystemAPI.GetComponentTypeHandle<AttackTarget>(),
                MoveOrder = SystemAPI.GetComponentTypeHandle<MoveOrderState>(),
            }.ScheduleParallel(_idle, state.Dependency);
        }

        // Writes only the dispatching unit's own components, so chunks with nothing queued keep their versions.
        [BurstCompile]
        private struct DispatchJob : IJobChunk
        {
            public ComponentTypeHandle<ActiveOrder> Active;
            public BufferTypeHandle<QueuedOrder> Queues;
            public ComponentTypeHandle<MoveDestination> Move;
            public ComponentTypeHandle<Arrival> Arrival;
            public ComponentTypeHandle<AttackTarget> Attack;
            public ComponentTypeHandle<MoveOrderState> MoveOrder;

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
                    Halt(chunk, i);
                    var active = chunk.GetNativeArray(ref Active);
                    active[i] = new ActiveOrder { Value = queues[i][0].Value };
                    chunk.GetBufferAccessorRW(ref Queues)[i].RemoveAt(0);
                    chunk.SetComponentEnabled(ref Active, i, true);
                }
            }

            // So a walk-up never reads the last order's arrival as its own.
            private void Halt(in ArchetypeChunk chunk, int index)
            {
                DisableIfPresent(chunk, index, ref Move);
                DisableIfPresent(chunk, index, ref Arrival);
                DisableIfPresent(chunk, index, ref Attack);
                DisableIfPresent(chunk, index, ref MoveOrder);
            }

            private static void DisableIfPresent<T>(in ArchetypeChunk chunk, int index, ref ComponentTypeHandle<T> handle)
                where T : unmanaged, IComponentData, IEnableableComponent
            {
                if (chunk.Has(ref handle))
                {
                    chunk.SetComponentEnabled(ref handle, index, false);
                }
            }
        }
    }
}
