using HyperRTS.Core;
using HyperRTS.Simulation.Navigation;
using Unity.Burst;
using Unity.Entities;
using Unity.Transforms;

namespace HyperRTS.Simulation.Combat
{
    /// <summary>Drops <see cref="FallingRemains"/> to the ground under gravity and leaves them lying there.</summary>
    [BurstCompile]
    [UpdateInGroup(typeof(LifecycleSystemGroup))]
    [UpdateAfter(typeof(DeathSystem))]
    public partial struct RemainsFallSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<FallingRemains>();
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // Without a heightfield the default one reads flat ground at y = 0.
            SystemAPI.TryGetSingleton(out TerrainHeight terrain);
            new FallJob
            {
                Terrain = terrain,
                DeltaTime = SystemAPI.Time.DeltaTime,
                Ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                    .CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter(),
            }.ScheduleParallel();
        }

        [BurstCompile]
        private partial struct FallJob : IJobEntity
        {
            public TerrainHeight Terrain;
            public float DeltaTime;
            public EntityCommandBuffer.ParallelWriter Ecb;

            private void Execute([ChunkIndexInQuery] int sortKey, Entity entity, ref LocalTransform transform,
                ref FallingRemains falling)
            {
                var ground = Terrain.Height(transform.Position.xz);
                if (falling.Fall(ref transform.Position.y, ground, DeltaTime))
                {
                    Ecb.RemoveComponent<FallingRemains>(sortKey, entity);
                }
            }
        }
    }
}
