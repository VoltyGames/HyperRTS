using HyperRTS.Core;
using Unity.Burst;
using Unity.Entities;

namespace HyperRTS.Simulation.Health
{
    /// <summary>
    /// Destroys any entity whose <see cref="HealthComponent.CurrentHealth"/> has dropped to zero or
    /// below. Runs last in the simulation; destruction is recorded from a parallel job and played back
    /// by <see cref="EndSimulationEntityCommandBufferSystem"/>, so there's no mid-frame sync point.
    /// </summary>
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
