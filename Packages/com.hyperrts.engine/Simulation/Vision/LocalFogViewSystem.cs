using HyperRTS.Core;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Match;
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
            var view = Evaluate(ref state, out var fog, out var relations);
            ref var published = ref SystemAPI.GetSingletonRW<LocalFogView>().ValueRW;
            var changed = view.Active != published.Active || view.Viewer != published.Viewer ||
                          (view.Active && fog.Version != _fogVersion);

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
            state.Dependency = view.Active
                ? new HideJob
                {
                    Fog = fog,
                    Relations = relations,
                    Viewer = view.Viewer,
                    Hidden = SystemAPI.GetComponentLookup<FogHidden>(true),
                    Commands = commands,
                }.ScheduleParallel(state.Dependency)
                : new RevealAllJob { Commands = commands }.ScheduleParallel(state.Dependency);
        }

        private LocalFogView Evaluate(ref SystemState state, out FogOfWar fog, out FactionRelations relations)
        {
            fog = default;
            relations = default;
            var enabled = SystemAPI.TryGetSingleton(out MapSettings map) && map.FogOfWar
                && SystemAPI.TryGetSingleton(out fog) && fog.IsCreated
                && SystemAPI.TryGetSingleton(out relations);

            var view = new LocalFogView { Viewer = Faction.Neutral };
            foreach (var player in SystemAPI.Query<RefRO<Player>>().WithAll<LocalPlayer>())
            {
                view.Viewer = player.ValueRO.Faction;
            }

            view.Team = relations.TeamOf(view.Viewer);
            view.Active = enabled && view.Team != 0;
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
            public EntityCommandBuffer.ParallelWriter Commands;

            private void Execute([ChunkIndexInQuery] int sortKey, Entity entity, in Faction faction,
                in LocalTransform transform)
            {
                var hide = Fog.IsHiddenFrom(in Relations, Viewer, faction.Value, transform.Position);
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
