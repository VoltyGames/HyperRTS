using HyperRTS.Core;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Resources
{
    /// <summary>Regrows renewable nodes toward their maximum and removes depleted finite ones.</summary>
    [BurstCompile]
    [UpdateInGroup(typeof(ProductionSystemGroup))]
    [UpdateAfter(typeof(GatherSystem))]
    public partial struct ResourceNodeSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state) =>
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);

            new NodeJob { DeltaTime = SystemAPI.Time.DeltaTime, Ecb = ecb.AsParallelWriter() }.ScheduleParallel();
        }

        [BurstCompile]
        private partial struct NodeJob : IJobEntity
        {
            public float DeltaTime;
            public EntityCommandBuffer.ParallelWriter Ecb;

            private void Execute([ChunkIndexInQuery] int sortKey, Entity entity, ref ResourceNode node)
            {
                if (node.RegrowthPerSecond <= 0f)
                {
                    if (node.Amount <= 0)
                    {
                        Ecb.DestroyEntity(sortKey, entity);
                    }

                    return;
                }

                if (node.Amount >= node.MaxAmount)
                {
                    node.RegrowthAccumulator = 0f;
                    return;
                }

                node.RegrowthAccumulator += node.RegrowthPerSecond * DeltaTime;
                var whole = (int)node.RegrowthAccumulator;
                node.RegrowthAccumulator -= whole;
                node.Amount = math.min(node.MaxAmount, node.Amount + whole);
            }
        }
    }
}
