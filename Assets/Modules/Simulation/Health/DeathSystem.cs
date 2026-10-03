using HyperRTS.Core;
using Unity.Burst;
using Unity.Entities;

namespace HyperRTS.Simulation.Health
{
    /// <summary>Destroys entities at zero health through the end-of-simulation command buffer.</summary>
    [BurstCompile]
    [UpdateInGroup(typeof(LifecycleSystemGroup))]
    public partial struct DeathSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);

            new DestroyDeadJob { Ecb = ecb.AsParallelWriter() }.ScheduleParallel();
        }

        [BurstCompile]
        private partial struct DestroyDeadJob : IJobEntity
        {
            public EntityCommandBuffer.ParallelWriter Ecb;

            private void Execute([ChunkIndexInQuery] int sortKey, Entity entity, in HealthComponent health)
            {
                if (health.CurrentHealth <= 0)
                {
                    Ecb.DestroyEntity(sortKey, entity);
                }
            }
        }
    }
}
