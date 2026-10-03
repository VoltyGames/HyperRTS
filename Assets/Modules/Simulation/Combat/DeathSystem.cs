using HyperRTS.Core;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;

namespace HyperRTS.Simulation.Combat
{
    /// <summary>
    /// Marks zero-health entities <see cref="Dead"/>, spawns their <see cref="SpawnOnDeath"/> prefab and destroys
    /// them at the end of the frame. Systems after this one in the lifecycle phase can react to <see cref="Dead"/>.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(LifecycleSystemGroup))]
    public partial struct DeathSystem : ISystem
    {
        private ComponentLookup<SpawnOnDeath> _spawnLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _spawnLookup = state.GetComponentLookup<SpawnOnDeath>(true);
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _spawnLookup.Update(ref state);
            var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);

            new DeathJob { Ecb = ecb.AsParallelWriter(), SpawnLookup = _spawnLookup }.ScheduleParallel();
        }

        [BurstCompile]
        [WithPresent(typeof(Dead))]
        private partial struct DeathJob : IJobEntity
        {
            public EntityCommandBuffer.ParallelWriter Ecb;
            [ReadOnly] public ComponentLookup<SpawnOnDeath> SpawnLookup;

            private void Execute([ChunkIndexInQuery] int sortKey, Entity entity, in Health health,
                in LocalTransform transform, EnabledRefRW<Dead> dead)
            {
                if (health.Current > 0f || dead.ValueRO)
                {
                    return;
                }

                dead.ValueRW = true;
                Ecb.DestroyEntity(sortKey, entity);

                if (SpawnLookup.TryGetComponent(entity, out var spawn))
                {
                    var remains = Ecb.Instantiate(sortKey, spawn.Prefab);
                    Ecb.SetComponent(sortKey, remains, LocalTransform.FromPositionRotation(transform.Position, transform.Rotation));
                }
            }
        }
    }
}
