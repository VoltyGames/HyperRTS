using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Systems;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Resources.Gathering
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

            new NodeJob
            {
                DeltaTime = SystemAPI.Time.DeltaTime,
                Ecb = ecb.AsParallelWriter(),
                Groups = SystemAPI.GetBufferLookup<LinkedEntityGroup>(true),
                Children = SystemAPI.GetBufferLookup<Child>(true),
            }.ScheduleParallel();
        }

        [BurstCompile]
        private partial struct NodeJob : IJobEntity
        {
            public float DeltaTime;
            public EntityCommandBuffer.ParallelWriter Ecb;
            [ReadOnly] public BufferLookup<LinkedEntityGroup> Groups;
            [ReadOnly] public BufferLookup<Child> Children;

            private void Execute([ChunkIndexInQuery] int sortKey, Entity entity, ref ResourceNode node)
            {
                if (node.RegrowthPerSecond <= 0f)
                {
                    if (node.Amount <= 0)
                    {
                        EntityParts.Destroy(entity, Groups, Children, Ecb, sortKey);
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
