using HyperRTS.Core;
using HyperRTS.Simulation.Common;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using Unity.Transforms;

namespace HyperRTS.Simulation.Vision
{
    /// <summary>
    /// Keeps a <see cref="LastSeen"/> memory of each enemy <see cref="FogMemory"/> root the local player saw and lost
    /// in the fog, until he sees it alive again or finds its place empty. Cleared on reveal-all, fog off or a viewer
    /// change.
    /// </summary>
    [BurstCompile]
    [WorldSystemFilter(SimulationWorlds.Presented)]
    [UpdateInGroup(typeof(CombatSystemGroup), OrderFirst = true)]
    [UpdateAfter(typeof(LocalFogViewSystem))]
    public partial struct FogMemorySystem : ISystem
    {
        /// <summary>Seconds a memory stays in sight waiting for its building, which a client gets a little later.</summary>
        public const float Grace = 1f;

        private byte _viewer;
        private bool _active;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<LocalFogView>();
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var view = SystemAPI.GetSingleton<LocalFogView>();
            if (!Track(ref state, view))
            {
                return;
            }

            var sight = new FogSight
            {
                Fog = SystemAPI.GetSingleton<FogOfWar>(),
                Team = view.Team,
                Commands = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                    .CreateCommandBuffer(state.WorldUnmanaged),
            };

            var seen = new NativeHashSet<FogMemoryKey>(64, state.WorldUpdateAllocator);
            state.Dependency = new SightJob
            {
                Sight = sight,
                Relations = SystemAPI.GetSingleton<FactionRelations>(),
                Viewer = view.Viewer,
                Seen = seen,
                Sightings = SystemAPI.GetComponentLookup<FogSighting>(),
                Cloaked = SystemAPI.GetComponentLookup<Stealthed>(true),
                Construction = SystemAPI.GetComponentLookup<ConstructionProgress>(true),
                Ghosts = SystemAPI.GetComponentLookup<GhostInstance>(true),
            }.Schedule(state.Dependency);
            state.Dependency = new OrphanJob { Sight = sight }.Schedule(state.Dependency);
            state.Dependency = new MemoryJob
            {
                Sight = sight,
                Seen = seen,
                DeltaTime = SystemAPI.Time.DeltaTime,
            }.Schedule(state.Dependency);
        }

        /// <summary>Whether the view hides anything; memories of an earlier view are forgotten first.</summary>
        private bool Track(ref SystemState state, in LocalFogView view)
        {
            if (view.Active != _active || view.Viewer != _viewer)
            {
                state.CompleteDependency();
                Forget(state.EntityManager);
                _active = view.Active;
                _viewer = view.Viewer;
            }

            return view.Active;
        }

        /// <summary>Drops every memory and sighting, for a view that no longer hides anything or belongs to someone else.</summary>
        public static void Forget(EntityManager entityManager)
        {
            using var memories = new EntityQueryBuilder(Allocator.Temp).WithAll<LastSeen>().Build(entityManager);
            entityManager.DestroyEntity(memories);
            using var sightings = new EntityQueryBuilder(Allocator.Temp).WithAll<FogSighting>().Build(entityManager);
            entityManager.RemoveComponent<FogSighting>(sightings);
        }

        private struct FogSight
        {
            [ReadOnly] public FogOfWar Fog;
            public byte Team;
            public EntityCommandBuffer Commands;

            public void Remember(in FogRecord record)
            {
                var memory = Commands.CreateEntity();
                Commands.AddComponent(memory, new LastSeen { Record = record });
            }
        }

        /// <summary>Records what the player sees of each enemy root and remembers the ones that just went out of sight.</summary>
        [BurstCompile]
        [WithAll(typeof(FogMemory))]
        [WithNone(typeof(Destructible))]
        private partial struct SightJob : IJobEntity
        {
            public FogSight Sight;
            public FactionRelations Relations;
            public byte Viewer;
            public NativeHashSet<FogMemoryKey> Seen;
            public ComponentLookup<FogSighting> Sightings;
            [ReadOnly] public ComponentLookup<Stealthed> Cloaked;
            [ReadOnly] public ComponentLookup<ConstructionProgress> Construction;
            [ReadOnly] public ComponentLookup<GhostInstance> Ghosts;

            private void Execute(Entity entity, in EntityInfo info, in Faction faction, in LocalTransform transform,
                in Health health)
            {
                var key = FogMemoryKey.Identify(entity, Ghosts);
                var hostile = Relations.IsHostile(Viewer, faction.Value);
                var visible = Sight.Fog.CanSee(Sight.Team, transform.Position, Cloaked.HasEnabled(entity));
                if (!hostile || visible)
                {
                    Seen.Add(key);
                    if (hostile)
                    {
                        Record(entity, Sighting(entity, key, info, faction, transform, health));
                    }

                    return;
                }

                if (Sightings.TryGetComponent(entity, out var sighting))
                {
                    Sight.Remember(sighting.Record);
                    Sight.Commands.RemoveComponent<FogSighting>(entity);
                }
            }

            private FogRecord Sighting(Entity entity, FogMemoryKey key, in EntityInfo info, in Faction faction,
                in LocalTransform transform, in Health health)
            {
                Construction.TryGetComponent(entity, out var progress);
                return new FogRecord
                {
                    Key = key,
                    TypeId = info.TypeId,
                    Faction = faction.Value,
                    Transform = transform,
                    Health = health.Fraction,
                    Construction = progress.Value,
                };
            }

            private void Record(Entity entity, in FogRecord record)
            {
                if (Sightings.HasComponent(entity))
                {
                    Sightings[entity] = new FogSighting { Record = record };
                    return;
                }

                Sight.Commands.AddComponent(entity, new FogSighting { Record = record });
            }
        }

        /// <summary>
        /// A sighted root that is gone was destroyed in plain view, or left a client's view with its ghost: only the
        /// second is remembered.
        /// </summary>
        [BurstCompile]
        [WithNone(typeof(EntityInfo))]
        private partial struct OrphanJob : IJobEntity
        {
            public FogSight Sight;

            private void Execute(Entity entity, in FogSighting sighting)
            {
                if (!Sight.Fog.IsVisible(sighting.Record.Transform.Position, Sight.Team))
                {
                    Sight.Remember(sighting.Record);
                }

                Sight.Commands.RemoveComponent<FogSighting>(entity);
            }
        }

        /// <summary>A memory goes once its building is seen alive, or its place has been in sight past the grace.</summary>
        [BurstCompile]
        private partial struct MemoryJob : IJobEntity
        {
            public FogSight Sight;
            [ReadOnly] public NativeHashSet<FogMemoryKey> Seen;
            public float DeltaTime;

            private void Execute(Entity entity, ref LastSeen memory)
            {
                if (Seen.Contains(memory.Record.Key))
                {
                    Sight.Commands.DestroyEntity(entity);
                    return;
                }

                var inSight = Sight.Fog.IsVisible(memory.Record.Transform.Position, Sight.Team);
                memory.VisibleFor = inSight ? memory.VisibleFor + DeltaTime : 0f;
                if (memory.VisibleFor >= Grace)
                {
                    Sight.Commands.DestroyEntity(entity);
                }
            }
        }
    }
}
