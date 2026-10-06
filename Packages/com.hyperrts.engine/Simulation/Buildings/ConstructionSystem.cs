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
        private ComponentLookup<ConstructionProgress> _siteLookup;
        private ComponentLookup<Producible> _producibleLookup;
        private ComponentLookup<LocalTransform> _transformLookup;
        private ComponentLookup<NavObstacle> _obstacleLookup;
        private ComponentLookup<Faction> _factionLookup;
        private SoundWriter _sounds;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _siteLookup = state.GetComponentLookup<ConstructionProgress>();
            _producibleLookup = state.GetComponentLookup<Producible>(true);
            _transformLookup = state.GetComponentLookup<LocalTransform>(true);
            _obstacleLookup = state.GetComponentLookup<NavObstacle>(true);
            _factionLookup = state.GetComponentLookup<Faction>(true);
            _sounds = new SoundWriter(ref state);
            state.RequireForUpdate<FactionRelations>();
            state.RequireForUpdate<SoundQueue>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _siteLookup.Update(ref state);
            _producibleLookup.Update(ref state);
            _transformLookup.Update(ref state);
            _obstacleLookup.Update(ref state);
            _factionLookup.Update(ref state);
            _sounds.Update(ref state, SystemAPI.GetSingletonEntity<SoundQueue>());

            new BuildJob
            {
                DeltaTime = SystemAPI.Time.DeltaTime,
                Relations = SystemAPI.GetSingleton<FactionRelations>(),
                SiteLookup = _siteLookup,
                ProducibleLookup = _producibleLookup,
                TransformLookup = _transformLookup,
                ObstacleLookup = _obstacleLookup,
                FactionLookup = _factionLookup,
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
            [ReadOnly] public ComponentLookup<LocalTransform> TransformLookup;
            [ReadOnly] public ComponentLookup<NavObstacle> ObstacleLookup;
            [ReadOnly] public ComponentLookup<Faction> FactionLookup;
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

                var sitePosition = TransformLookup[site].Position;
                var extents = ReachMath.HalfExtents(ObstacleLookup, site);
                if (!ReachMath.Approach(ref destination, moving, transform.Position, agent.Radius, sitePosition, extents))
                {
                    return;
                }

                if (Advance(site, builder.Rate))
                {
                    busy.ValueRW = false;
                    Sounds.Play(site, SoundSlot.Ready, sitePosition, FactionLookup[site].Value);
                }
            }

            private bool IsAlliedSite(Entity site, byte faction)
            {
                if (!TransformLookup.HasComponent(site))
                {
                    return false;
                }

                return BuildingRules.IsAlliedSite(SiteLookup, FactionLookup, Relations, site, faction);
            }

            /// <summary>Adds this frame's work and returns true once the site is finished.</summary>
            private bool Advance(Entity site, float rate)
            {
                ref var progress = ref SiteLookup.GetRefRW(site).ValueRW;
                progress.Value += DeltaTime * rate / Producible.BuildTimeOf(ProducibleLookup, site);
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
