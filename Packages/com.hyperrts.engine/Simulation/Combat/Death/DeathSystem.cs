using HyperRTS.Core;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Cues;
using HyperRTS.Simulation.Destruction;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Combat.Death
{
    /// <summary>
    /// Marks zero-health entities <see cref="Dead"/>, spawns their remains and destroys them at the end of the frame.
    /// <see cref="SceneryCleared"/> ones go quietly: no remains and no Dead, so no cues.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(LifecycleSystemGroup))]
    [UpdateBefore(typeof(DeathCueSystem))]
    public partial struct DeathSystem : ISystem
    {
        private ComponentLookup<SpawnOnDeath> _spawnLookup;
        private RemainsLookups _remains;
        private ComponentLookup<DropRemains> _drops;
        private ComponentLookup<TurretToss> _tosses;
        private ComponentLookup<LethalHit> _lethalHits;
        private BufferLookup<TossType> _tossTypes;
        private ComponentLookup<SceneryId> _ids;
        private BufferLookup<LinkedEntityGroup> _groups;
        private BufferLookup<Child> _children;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _spawnLookup = state.GetComponentLookup<SpawnOnDeath>(true);
            _remains = RemainsLookups.Create(ref state);
            _drops = state.GetComponentLookup<DropRemains>(true);
            _tosses = state.GetComponentLookup<TurretToss>(true);
            _lethalHits = state.GetComponentLookup<LethalHit>(true);
            _tossTypes = state.GetBufferLookup<TossType>(true);
            _ids = state.GetComponentLookup<SceneryId>(true);
            _groups = state.GetBufferLookup<LinkedEntityGroup>(true);
            _children = state.GetBufferLookup<Child>(true);
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _spawnLookup.Update(ref state);
            _remains.Update(ref state);
            _drops.Update(ref state);
            _tosses.Update(ref state);
            _lethalHits.Update(ref state);
            _tossTypes.Update(ref state);
            _ids.Update(ref state);
            _groups.Update(ref state);
            _children.Update(ref state);
            var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter();

            state.Dependency = new DeathJob
            {
                Ecb = ecb, SpawnLookup = _spawnLookup, RemainsLookups = _remains,
                Drops = _drops,
                Tosses = _tosses, LethalHits = _lethalHits, TossTypes = _tossTypes, Ids = _ids, Groups = _groups, Children = _children,
            }.ScheduleParallel(state.Dependency);
            state.Dependency = new ClearJob { Ecb = ecb, Groups = _groups, Children = _children }
                .ScheduleParallel(state.Dependency);
        }

        [BurstCompile]
        [WithPresent(typeof(Dead))]
        [WithNone(typeof(SceneryCleared))]
        [WithChangeFilter(typeof(Health))]
        private partial struct DeathJob : IJobEntity
        {
            public EntityCommandBuffer.ParallelWriter Ecb;
            [ReadOnly] public ComponentLookup<SpawnOnDeath> SpawnLookup;
            [ReadOnly] public RemainsLookups RemainsLookups;
            [ReadOnly] public ComponentLookup<DropRemains> Drops;
            [ReadOnly] public ComponentLookup<TurretToss> Tosses;
            [ReadOnly] public ComponentLookup<LethalHit> LethalHits;
            [ReadOnly] public BufferLookup<TossType> TossTypes;
            [ReadOnly] public ComponentLookup<SceneryId> Ids;
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
                EntityParts.Destroy(entity, Groups, Children, Ecb, sortKey);
                if (SpawnLookup.TryGetComponent(entity, out var spawn))
                {
                    var thrown = Tosses.TryGetComponent(entity, out var toss) && Catastrophic(entity, toss, health);
                    SpawnRemains(sortKey, entity, thrown ? toss.Hull : spawn.Prefab, transform);
                    if (thrown)
                    {
                        ThrowTurret(sortKey, toss, LethalHits[entity], transform);
                    }
                }
            }

            private bool Catastrophic(Entity entity, in TurretToss toss, in Health health) =>
                toss.IsCatastrophic(LethalHits[entity], TossTypes[entity], health.Max);

            /// <summary>The turret lands as small remains at a seeded point, blocking its cell; presentation flies it there.</summary>
            private void ThrowTurret(int sortKey, in TurretToss toss, in LethalHit hit, in LocalTransform hull)
            {
                var tossed = toss.Throw(hull.Position, hit.Heading, out var landing);
                var turret = Ecb.Instantiate(sortKey, toss.Turret);
                Ecb.Place(sortKey, turret, hull.WithPosition(landing), float4x4.identity);
                Ecb.SetComponent(sortKey, turret, tossed);
                Ecb.AddComponent<FallingRemains>(sortKey, turret);
            }

            /// <summary>
            /// Remains copy the dead piece's transform and stretch, except toppling ones, which lie away from the lethal
            /// hit, and dropped ones, which fall to the ground.
            /// </summary>
            private void SpawnRemains(int sortKey, Entity entity, Entity prefab, in LocalTransform transform)
            {
                // Only Topple pieces and turret tosses carry a LethalHit, and only Topple pieces read the heading.
                LethalHits.TryGetComponent(entity, out var fell);
                var spawned = RemainsLookups.Spawn(Ecb, sortKey, entity, prefab, transform, fell.Heading, true);
                if (Drops.HasComponent(entity))
                {
                    Ecb.AddComponent<FallingRemains>(sortKey, spawned);
                }

                // Remains of map scenery answer to its id, so multiplayer can clear them.
                if (Ids.TryGetComponent(entity, out var id))
                {
                    Ecb.AddComponent(sortKey, spawned, id);
                }
            }
        }

        [BurstCompile]
        [WithAll(typeof(SceneryCleared))]
        private partial struct ClearJob : IJobEntity
        {
            public EntityCommandBuffer.ParallelWriter Ecb;
            [ReadOnly] public BufferLookup<LinkedEntityGroup> Groups;
            [ReadOnly] public BufferLookup<Child> Children;

            private void Execute([ChunkIndexInQuery] int sortKey, Entity entity)
            {
                EntityParts.Destroy(entity, Groups, Children, Ecb, sortKey);
            }
        }
    }
}
