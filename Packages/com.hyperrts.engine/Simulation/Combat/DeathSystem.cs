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
        private ComponentLookup<PostTransformMatrix> _stretches;
        private ComponentLookup<Topple> _topples;
        private ComponentLookup<FellFrom> _fellFrom;
        private BufferLookup<LinkedEntityGroup> _groups;
        private BufferLookup<Child> _children;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _spawnLookup = state.GetComponentLookup<SpawnOnDeath>(true);
            _stretches = state.GetComponentLookup<PostTransformMatrix>(true);
            _topples = state.GetComponentLookup<Topple>(true);
            _fellFrom = state.GetComponentLookup<FellFrom>(true);
            _groups = state.GetBufferLookup<LinkedEntityGroup>(true);
            _children = state.GetBufferLookup<Child>(true);
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _spawnLookup.Update(ref state);
            _stretches.Update(ref state);
            _topples.Update(ref state);
            _fellFrom.Update(ref state);
            _groups.Update(ref state);
            _children.Update(ref state);
            var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);

            new DeathJob
            {
                Ecb = ecb.AsParallelWriter(), SpawnLookup = _spawnLookup, Stretches = _stretches, Topples = _topples,
                FallSources = _fellFrom, Groups = _groups, Children = _children,
            }.ScheduleParallel();
        }

        [BurstCompile]
        [WithPresent(typeof(Dead))]
        [WithChangeFilter(typeof(Health))]
        private partial struct DeathJob : IJobEntity
        {
            public EntityCommandBuffer.ParallelWriter Ecb;
            [ReadOnly] public ComponentLookup<SpawnOnDeath> SpawnLookup;
            [ReadOnly] public ComponentLookup<PostTransformMatrix> Stretches;
            [ReadOnly] public ComponentLookup<Topple> Topples;
            [ReadOnly] public ComponentLookup<FellFrom> FallSources;
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
                if (SpawnLookup.TryGetComponent(entity, out var spawn))
                {
                    SpawnRemains(sortKey, entity, spawn.Prefab, transform);
                }
            }

            /// <summary>
            /// Remains stand, turn and scale like what died (stretch included), so a big tree leaves a big trunk; a
            /// toppling piece's remains lie away from its lethal hit instead.
            /// </summary>
            private void SpawnRemains(int sortKey, Entity entity, Entity prefab, in LocalTransform transform)
            {
                var remains = Ecb.Instantiate(sortKey, prefab);
                Ecb.SetComponent(sortKey, remains, transform);
                if (Stretches.TryGetComponent(entity, out var stretch))
                {
                    Ecb.AddComponent(sortKey, remains, stretch);
                }

                if (Topples.TryGetComponent(entity, out var topple))
                {
                    var fall = ToppleMath.Quantize(FallSources[entity].Position, transform.Position);
                    var felling = ToppleMath.Fell(transform, topple, fall, true, out var pose);
                    Ecb.SetComponent(sortKey, remains, pose);
                    Ecb.AddComponent(sortKey, remains, felling);
                }
            }
        }
    }
}
