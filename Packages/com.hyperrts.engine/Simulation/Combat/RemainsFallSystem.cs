using HyperRTS.Core;
using HyperRTS.Simulation.Navigation;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;

namespace HyperRTS.Simulation.Combat
{
    /// <summary>
    /// Drops <see cref="FallingRemains"/> under gravity onto the <see cref="NavGrid"/> surface (ground, water or deck)
    /// and leaves them lying there.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(LifecycleSystemGroup))]
    [UpdateAfter(typeof(DeathSystem))]
    public partial struct RemainsFallSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<FallingRemains>();
            state.RequireForUpdate<NavGrid>();
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            new FallJob
            {
                Grid = SystemAPI.GetSingleton<NavGrid>(),
                DeltaTime = SystemAPI.Time.DeltaTime,
                Ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                    .CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter(),
            }.ScheduleParallel();
        }

        [BurstCompile]
        private partial struct FallJob : IJobEntity
        {
            [ReadOnly] public NavGrid Grid;
            public float DeltaTime;
            public EntityCommandBuffer.ParallelWriter Ecb;

            private void Execute([ChunkIndexInQuery] int sortKey, Entity entity, ref LocalTransform transform,
                ref FallingRemains falling)
            {
                var ground = Grid.SurfaceHeight(transform.Position);
                if (falling.Fall(ref transform.Position.y, ground, DeltaTime))
                {
                    Ecb.RemoveComponent<FallingRemains>(sortKey, entity);
                }
            }
        }
    }
}
