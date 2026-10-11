using HyperRTS.Core;
using HyperRTS.Simulation.Audio;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Navigation;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Production;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;

namespace HyperRTS.Simulation.Buildings
{
    /// <summary>
    /// Runs Build orders: builders walk to an allied site and add <c>Rate / BuildTime</c> progress per second while
    /// in reach. Completion disables <see cref="ConstructionProgress"/>; sites never progress on their own.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(ProductionSystemGroup))]
    public partial struct ConstructionSystem : ISystem
    {
        private EntityQuery _players;
        private ComponentLookup<ConstructionProgress> _siteLookup;
        private ComponentLookup<Producible> _producibleLookup;
        private TargetReach _reach;
        private ComponentLookup<Faction> _factionLookup;
        private ComponentLookup<PlayerStats> _statsLookup;
        private SoundWriter _sounds;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _players = SystemAPI.QueryBuilder().WithAll<Player>().Build();
            _siteLookup = state.GetComponentLookup<ConstructionProgress>();
            _producibleLookup = state.GetComponentLookup<Producible>(true);
            _reach = new TargetReach(ref state);
            _factionLookup = state.GetComponentLookup<Faction>(true);
            _statsLookup = state.GetComponentLookup<PlayerStats>();
            _sounds = new SoundWriter(ref state);
            state.RequireForUpdate<FactionRelations>();
            state.RequireForUpdate<SoundQueue>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _siteLookup.Update(ref state);
            _producibleLookup.Update(ref state);
            _reach.Update(ref state);
            _factionLookup.Update(ref state);
            _statsLookup.Update(ref state);
            _sounds.Update(ref state, SystemAPI.GetSingletonEntity<SoundQueue>());

            new BuildJob
            {
                DeltaTime = SystemAPI.Time.DeltaTime,
                Relations = SystemAPI.GetSingleton<FactionRelations>(),
                SiteLookup = _siteLookup,
                ProducibleLookup = _producibleLookup,
                Reach = _reach,
                FactionLookup = _factionLookup,
                PlayerByFaction = PlayerLookup.ByFaction(_players, state.WorldUpdateAllocator),
                StatsLookup = _statsLookup,
                Sounds = _sounds,
            }.Schedule();
        }

        /// <summary>Single-threaded so several builders can add to one site.</summary>
        [BurstCompile]
        [WithNone(typeof(Dead))]
        [WithPresent(typeof(MoveDestination))]
        private partial struct BuildJob : IJobEntity
        {
            public float DeltaTime;
            public FactionRelations Relations;
            public ComponentLookup<ConstructionProgress> SiteLookup;
            [ReadOnly] public ComponentLookup<Producible> ProducibleLookup;
            public TargetReach Reach;
            [ReadOnly] public ComponentLookup<Faction> FactionLookup;
            [ReadOnly] public NativeArray<Entity> PlayerByFaction;
            public ComponentLookup<PlayerStats> StatsLookup;
            public SoundWriter Sounds;

            private void Execute(in Builder builder, ref ActiveOrder order, EnabledRefRW<ActiveOrder> busy,
                ref MoveDestination destination, EnabledRefRW<MoveDestination> moving, in LocalTransform transform,
                in NavAgent agent, in Faction faction)
            {
                if (!busy.ValueRO || order.Value.Type != OrderType.Build)
                {
                    return;
                }

                var site = order.Value.Target;
                if (!IsAlliedSite(site, faction.Value))
                {
                    ActiveOrder.Finish(busy, moving);
                    return;
                }

                var sitePosition = Reach.Position(site);
                if (!Reach.Approach(site, transform.Position, agent.Radius, ref destination, moving))
                {
                    return;
                }

                if (Advance(site, builder.Rate))
                {
                    busy.ValueRW = false;
                    Sounds.Play(site, SoundSlot.Ready, sitePosition, FactionLookup[site].Value);
                    CountBuilt(FactionLookup[site].Value);
                }
            }

            private bool IsAlliedSite(Entity site, byte faction)
            {
                if (!Reach.Exists(site))
                {
                    return false;
                }

                return BuildingRules.IsAlliedSite(SiteLookup, FactionLookup, Relations, site, faction);
            }

            private void CountBuilt(byte faction)
            {
                if (StatsLookup.TryGetRefRW(PlayerByFaction[faction], out var stats))
                {
                    stats.ValueRW.BuildingsBuilt++;
                }
            }

            /// <summary>Adds this frame's work and returns true once the site is finished.</summary>
            private bool Advance(Entity site, float rate)
            {
                ref var progress = ref SiteLookup.GetRefRW(site).ValueRW;
                progress.Value += DeltaTime * rate / Producible.GetBuildTime(ProducibleLookup, site);
                if (progress.Value < 1f)
                {
                    return false;
                }

                progress.Value = 1f;
                SiteLookup.SetComponentEnabled(site, false);
                return true;
            }
        }
    }
}
