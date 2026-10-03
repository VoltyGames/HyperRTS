using HyperRTS.Core;
using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Match;
using HyperRTS.Simulation.Navigation;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Units;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
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
        private ComponentLookup<ConstructionProgress> _siteLookup;
        private ComponentLookup<Producible> _producibleLookup;
        private ComponentLookup<LocalTransform> _transformLookup;
        private ComponentLookup<NavObstacle> _obstacleLookup;
        private ComponentLookup<Faction> _factionLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _siteLookup = state.GetComponentLookup<ConstructionProgress>();
            _producibleLookup = state.GetComponentLookup<Producible>(true);
            _transformLookup = state.GetComponentLookup<LocalTransform>(true);
            _obstacleLookup = state.GetComponentLookup<NavObstacle>(true);
            _factionLookup = state.GetComponentLookup<Faction>(true);
            state.RequireForUpdate<FactionRelations>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _siteLookup.Update(ref state);
            _producibleLookup.Update(ref state);
            _transformLookup.Update(ref state);
            _obstacleLookup.Update(ref state);
            _factionLookup.Update(ref state);

            new BuildJob
            {
                DeltaTime = SystemAPI.Time.DeltaTime,
                Relations = SystemAPI.GetSingleton<FactionRelations>(),
                SiteLookup = _siteLookup,
                ProducibleLookup = _producibleLookup,
                TransformLookup = _transformLookup,
                ObstacleLookup = _obstacleLookup,
                FactionLookup = _factionLookup,
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
            [ReadOnly] public ComponentLookup<LocalTransform> TransformLookup;
            [ReadOnly] public ComponentLookup<NavObstacle> ObstacleLookup;
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
                    busy.ValueRW = false;
                    moving.ValueRW = false;
                    return;
                }

                var sitePosition = TransformLookup[site].Position;
                var extents = ReachMath.HalfExtents(ObstacleLookup, site);
                if (!ReachMath.InReach(transform.Position, agent.Radius, sitePosition, extents))
                {
                    ReachMath.MoveTo(ref destination, moving, sitePosition);
                    return;
                }

                moving.ValueRW = false;
                if (Advance(site, builder.Rate))
                {
                    busy.ValueRW = false;
                }
            }

            private bool IsAlliedSite(Entity site, byte faction) =>
                SiteLookup.HasComponent(site) && SiteLookup.IsComponentEnabled(site) &&
                TransformLookup.HasComponent(site) && FactionLookup.TryGetComponent(site, out var owner) &&
                Relations.IsAllied(faction, owner.Value);

            /// <summary>Adds this frame's work and returns true once the site is finished.</summary>
            private bool Advance(Entity site, float rate)
            {
                var buildTime = ProducibleLookup.TryGetComponent(site, out var producible) ? producible.BuildTime : 0f;
                ref var progress = ref SiteLookup.GetRefRW(site).ValueRW;
                progress.Value += DeltaTime * rate / math.max(buildTime, 0.01f);

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
