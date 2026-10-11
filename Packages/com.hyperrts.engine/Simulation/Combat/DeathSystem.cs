using HyperRTS.Core;
using HyperRTS.Simulation.Audio;
using HyperRTS.Simulation.Common;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;

namespace HyperRTS.Simulation.Combat
{
    /// <summary>
    /// Marks zero-health entities <see cref="Dead"/>, spawns their <see cref="SpawnOnDeath"/> prefab in their place and
    /// destroys them whole (<see cref="EntityParts"/>) at the end of the frame. Systems after this one in the lifecycle
    /// phase can react to <see cref="Dead"/>.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(LifecycleSystemGroup))]
    [UpdateBefore(typeof(DeathSoundSystem))]
    public partial struct DeathSystem : ISystem
    {
        private ComponentLookup<SpawnOnDeath> _spawnLookup;
        private BufferLookup<LinkedEntityGroup> _groups;
        private BufferLookup<Child> _children;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _spawnLookup = state.GetComponentLookup<SpawnOnDeath>(true);
            _groups = state.GetBufferLookup<LinkedEntityGroup>(true);
            _children = state.GetBufferLookup<Child>(true);
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _spawnLookup.Update(ref state);
            _groups.Update(ref state);
            _children.Update(ref state);
            var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);

            new DeathJob
            {
                Ecb = ecb.AsParallelWriter(), SpawnLookup = _spawnLookup, Groups = _groups, Children = _children,
            }.ScheduleParallel();
        }

        [BurstCompile]
        [WithPresent(typeof(Dead))]
        [WithChangeFilter(typeof(Health))]
        private partial struct DeathJob : IJobEntity
        {
            public EntityCommandBuffer.ParallelWriter Ecb;
            [ReadOnly] public ComponentLookup<SpawnOnDeath> SpawnLookup;
            [ReadOnly] public BufferLookup<LinkedEntityGroup> Groups;
            [ReadOnly] public BufferLookup<Child> Children;

            private void Execute([ChunkIndexInQuery] int sortKey, Entity entity, in Health health,
                in LocalTransform transform, EnabledRefRW<Dead> dead)
            {
                if (health.Current > 0f || dead.ValueRO)
                {
                    return;
                }

                dead.ValueRW = true;
                var parts = new NativeList<Entity>(4, Allocator.Temp);
                EntityParts.Collect(entity, Groups, Children, parts);
                Ecb.DestroyEntity(sortKey, parts.AsArray());

                // Remains stand, turn and scale like what died, so a big tree leaves a big trunk.
                if (SpawnLookup.TryGetComponent(entity, out var spawn))
                {
                    var remains = Ecb.Instantiate(sortKey, spawn.Prefab);
                    Ecb.SetComponent(sortKey, remains, transform);
                }
            }
        }
    }
}
