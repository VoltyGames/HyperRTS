using HyperRTS.Core;
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
    /// in reach. Any site at full progress (built, or raised by a cheat or game effect) then completes: it disables
    /// <see cref="ConstructionProgress"/> and raises <see cref="BuildingCompleted"/>. Sites never progress on their own.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(ProductionSystemGroup))]
    public partial struct ConstructionSystem : ISystem
    {
        private ComponentLookup<ConstructionProgress> _siteLookup;
        private ComponentLookup<Producible> _producibleLookup;
        private TargetReach _reach;
        private ComponentLookup<Faction> _factionLookup;
        private BufferLookup<BuildingCompleted> _completedLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _siteLookup = state.GetComponentLookup<ConstructionProgress>();
            _producibleLookup = state.GetComponentLookup<Producible>(true);
            _reach = new TargetReach(ref state);
            state.RequireForUpdate<MapSettings>();
            _factionLookup = state.GetComponentLookup<Faction>(true);
            _completedLookup = state.GetBufferLookup<BuildingCompleted>();
            state.RequireForUpdate<FactionRelations>();
            state.RequireForUpdate<GameEvents>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _siteLookup.Update(ref state);
            _producibleLookup.Update(ref state);
            _reach.Update(ref state, SystemAPI.GetSingleton<MapSettings>());
            _factionLookup.Update(ref state);
            _completedLookup.Update(ref state);

            new BuildJob
            {
                DeltaTime = SystemAPI.Time.DeltaTime,
                Relations = SystemAPI.GetSingleton<FactionRelations>(),
                SiteLookup = _siteLookup,
                ProducibleLookup = _producibleLookup,
                Reach = _reach,
                FactionLookup = _factionLookup,
            }.Schedule();

            new FinishJob
            {
                Completed = _completedLookup,
                Events = SystemAPI.GetSingletonEntity<GameEvents>(),
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

                if (!Reach.Approach(site, transform, agent, ref destination, moving))
                {
                    return;
                }

                if (Advance(site, builder.Rate))
                {
                    busy.ValueRW = false;
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

            /// <summary>Adds this frame's work and returns true once the site is at full progress.</summary>
            private bool Advance(Entity site, float rate)
            {
                ref var progress = ref SiteLookup.GetRefRW(site).ValueRW;
                progress.Value += DeltaTime * rate / Producible.GetBuildTime(ProducibleLookup, site);
                if (progress.Value < 1f)
                {
                    return false;
                }

                progress.Value = 1f;
                return true;
            }
        }

        /// <summary>Completes every site at full progress, however it got there.</summary>
        [BurstCompile]
        [WithNone(typeof(Dead))]
        private partial struct FinishJob : IJobEntity
        {
            public BufferLookup<BuildingCompleted> Completed;
            public Entity Events;

            private void Execute(Entity site, ref ConstructionProgress progress,
                EnabledRefRW<ConstructionProgress> underConstruction, in Faction faction)
            {
                if (progress.Value < 1f)
                {
                    return;
                }

                underConstruction.ValueRW = false;
                Completed[Events].Add(new BuildingCompleted { Building = site, Faction = faction.Value });
            }
        }
    }
}
