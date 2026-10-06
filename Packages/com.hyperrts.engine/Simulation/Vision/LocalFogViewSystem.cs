using HyperRTS.Core;
using HyperRTS.Simulation.Common;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;

namespace HyperRTS.Simulation.Vision
{
    /// <summary>
    /// Publishes <see cref="LocalFogView"/> and, once per change, tags what the local player can't see with
    /// <see cref="FogHidden"/>, so every consumer reads one answer.
    /// </summary>
    [BurstCompile]
    [WorldSystemFilter(SimulationWorlds.Presented)]
    [UpdateInGroup(typeof(CombatSystemGroup), OrderFirst = true)]
    [UpdateAfter(typeof(FogOfWarSystem))]
    public partial struct LocalFogViewSystem : ISystem
    {
        private int _fogVersion;
        private int _factionOrder;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.EntityManager.CreateSingleton<LocalFogView>();
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var view = Evaluate(ref state, out var fog, out var relations, out var hides);
            ref var published = ref SystemAPI.GetSingletonRW<LocalFogView>().ValueRW;
            var restamped = hides && fog.Version != _fogVersion;
            var changed = restamped || view.Active != published.Active || view.Viewer != published.Viewer;

            // Faction's order version covers spawns, which must not show until the next restamp.
            var factionOrder = state.EntityManager.GetComponentOrderVersion<Faction>();
            if (!changed && factionOrder == _factionOrder)
            {
                return;
            }

            _factionOrder = factionOrder;
            if (changed)
            {
                _fogVersion = fog.Version;
                view.Version = published.Version + 1;
                published = view;
            }

            var commands = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter();
            state.Dependency = hides
                ? new HideJob
                {
                    Fog = fog,
                    Relations = relations,
                    Viewer = view.Viewer,
                    Hidden = SystemAPI.GetComponentLookup<FogHidden>(true),
                    Cloaked = SystemAPI.GetComponentLookup<Stealthed>(true),
                    Commands = commands,
                }.ScheduleParallel(state.Dependency)
                : new RevealAllJob { Commands = commands }.ScheduleParallel(state.Dependency);
        }

        /// <summary>
        /// Shows the local player everything, or stops doing so. Applied at once because the gameplay phases this
        /// system runs in may be paused (replays).
        /// </summary>
        public static void SetRevealAll(EntityManager entityManager, bool reveal)
        {
            using var views = entityManager.CreateEntityQuery(ComponentType.ReadWrite<LocalFogView>());
            if (!views.TryGetSingletonRW<LocalFogView>(out var view))
            {
                return;
            }

            view.ValueRW.RevealAll = reveal;
            view.ValueRW.Active &= !reveal;
            view.ValueRW.Version++;
            if (reveal)
            {
                using var hidden = entityManager.CreateEntityQuery(ComponentType.ReadOnly<FogHidden>());
                entityManager.RemoveComponent<FogHidden>(hidden);
            }
        }

        /// <summary>
        /// The view is active with fog on; entities are hidden whenever there is a team to see as (stealth), unless
        /// everything is revealed.
        /// </summary>
        private LocalFogView Evaluate(ref SystemState state, out FogOfWar fog, out FactionRelations relations,
            out bool hides)
        {
            var revealAll = SystemAPI.GetSingleton<LocalFogView>().RevealAll;
            var view = new LocalFogView { Viewer = Faction.Neutral, RevealAll = revealAll };
            foreach (var player in SystemAPI.Query<RefRO<Player>>().WithAll<LocalPlayer>())
            {
                view.Viewer = player.ValueRO.Faction;
            }

            SystemAPI.TryGetSingleton(out relations);
            SystemAPI.TryGetSingleton(out fog);
            view.Team = relations.TeamOf(view.Viewer);
            hides = view.Team != 0 && fog.IsCreated && !revealAll;

            var fogOn = SystemAPI.TryGetSingleton(out MapSettings map) && map.FogOfWar;
            view.Active = hides && fogOn;
            return view;
        }

        [BurstCompile]
        [WithAll(typeof(EntityInfo))]
        private partial struct HideJob : IJobEntity
        {
            [ReadOnly] public FogOfWar Fog;
            public FactionRelations Relations;
            public byte Viewer;
            [ReadOnly] public ComponentLookup<FogHidden> Hidden;
            [ReadOnly] public ComponentLookup<Stealthed> Cloaked;
            public EntityCommandBuffer.ParallelWriter Commands;

            private void Execute([ChunkIndexInQuery] int sortKey, Entity entity, in Faction faction,
                in LocalTransform transform)
            {
                var stealthed = Cloaked.HasEnabled(entity);
                var hide = Fog.IsHiddenFrom(in Relations, Viewer, faction.Value, transform.Position, stealthed);
                if (hide == Hidden.HasComponent(entity))
                {
                    return;
                }

                if (hide)
                {
                    Commands.AddComponent<FogHidden>(sortKey, entity);
                }
                else
                {
                    Commands.RemoveComponent<FogHidden>(sortKey, entity);
                }
            }
        }

        [BurstCompile]
        [WithAll(typeof(FogHidden))]
        private partial struct RevealAllJob : IJobEntity
        {
            public EntityCommandBuffer.ParallelWriter Commands;

            private void Execute([ChunkIndexInQuery] int sortKey, Entity entity) =>
                Commands.RemoveComponent<FogHidden>(sortKey, entity);
        }
    }
}
