using HyperRTS.Core;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Cues;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;

namespace HyperRTS.Simulation.Combat
{
    /// <summary>Marks zero-health entities <see cref="Dead"/>, spawns their remains and destroys them at the end of the frame.</summary>
    [BurstCompile]
    [UpdateInGroup(typeof(LifecycleSystemGroup))]
    [UpdateBefore(typeof(DeathCueSystem))]
    public partial struct DeathSystem : ISystem
    {
        private ComponentLookup<SpawnOnDeath> _spawnLookup;
        private RemainsLookups _remains;
        private ComponentLookup<ToppleHeading> _toppleHeadings;
        private ComponentLookup<DropRemains> _drops;
        private BufferLookup<LinkedEntityGroup> _groups;
        private BufferLookup<Child> _children;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _spawnLookup = state.GetComponentLookup<SpawnOnDeath>(true);
            _remains = RemainsLookups.Create(ref state);
            _toppleHeadings = state.GetComponentLookup<ToppleHeading>(true);
            _drops = state.GetComponentLookup<DropRemains>(true);
            _groups = state.GetBufferLookup<LinkedEntityGroup>(true);
            _children = state.GetBufferLookup<Child>(true);
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _spawnLookup.Update(ref state);
            _remains.Update(ref state);
            _toppleHeadings.Update(ref state);
            _drops.Update(ref state);
            _groups.Update(ref state);
            _children.Update(ref state);
            var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);

            new DeathJob
            {
                Ecb = ecb.AsParallelWriter(), SpawnLookup = _spawnLookup, RemainsLookups = _remains,
                FallSources = _toppleHeadings, Drops = _drops, Groups = _groups, Children = _children,
            }.ScheduleParallel();
        }

        [BurstCompile]
        [WithPresent(typeof(Dead))]
        [WithChangeFilter(typeof(Health))]
        private partial struct DeathJob : IJobEntity
        {
            public EntityCommandBuffer.ParallelWriter Ecb;
            [ReadOnly] public ComponentLookup<SpawnOnDeath> SpawnLookup;
            [ReadOnly] public RemainsLookups RemainsLookups;
            [ReadOnly] public ComponentLookup<ToppleHeading> FallSources;
            [ReadOnly] public ComponentLookup<DropRemains> Drops;
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
            /// Remains copy the dead piece's transform and stretch, except toppling ones, which lie away from the lethal
            /// hit, and dropped ones, which fall to the ground.
            /// </summary>
            private void SpawnRemains(int sortKey, Entity entity, Entity prefab, in LocalTransform transform)
            {
                // Only Topple pieces carry ToppleHeading, and only they read the heading.
                FallSources.TryGetComponent(entity, out var fell);
                var remains = Remains.FromPiece(entity, transform, fell.Heading, true, RemainsLookups);
                var spawned = Ecb.Instantiate(sortKey, prefab);
                Ecb.Place(sortKey, spawned, remains.Transform, remains.StretchMatrix);
                if (remains.Stretched)
                {
                    Ecb.AddComponent(sortKey, spawned, remains.Stretch);
                }

                if (remains.PartsStretched)
                {
                    Ecb.AddComponent(sortKey, spawned, remains.Parts);
                }

                if (remains.Felled)
                {
                    Ecb.AddComponent(sortKey, spawned, remains.ToppleFall);
                }

                if (Drops.HasComponent(entity))
                {
                    Ecb.AddComponent<FallingRemains>(sortKey, spawned);
                }
            }
        }
    }
}
