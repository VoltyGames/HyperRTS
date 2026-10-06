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
    /// Runs Repair orders: builders walk to an allied finished building and restore health as fast as they would
    /// build it (<c>Rate × MaxHealth / BuildTime</c> per second). The order ends once it is whole or gone.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(ProductionSystemGroup))]
    public partial struct RepairSystem : ISystem
    {
        private ComponentLookup<Health> _healthLookup;
        private ComponentLookup<Producible> _producibleLookup;
        private ComponentLookup<LocalTransform> _transformLookup;
        private ComponentLookup<NavObstacle> _obstacleLookup;
        private ComponentLookup<Faction> _factionLookup;
        private ComponentLookup<ConstructionProgress> _siteLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _healthLookup = state.GetComponentLookup<Health>();
            _producibleLookup = state.GetComponentLookup<Producible>(true);
            _transformLookup = state.GetComponentLookup<LocalTransform>(true);
            _obstacleLookup = state.GetComponentLookup<NavObstacle>(true);
            _factionLookup = state.GetComponentLookup<Faction>(true);
            _siteLookup = state.GetComponentLookup<ConstructionProgress>(true);
            state.RequireForUpdate<FactionRelations>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _healthLookup.Update(ref state);
            _producibleLookup.Update(ref state);
            _transformLookup.Update(ref state);
            _obstacleLookup.Update(ref state);
            _factionLookup.Update(ref state);
            _siteLookup.Update(ref state);

            new RepairJob
            {
                DeltaTime = SystemAPI.Time.DeltaTime,
                Relations = SystemAPI.GetSingleton<FactionRelations>(),
                HealthLookup = _healthLookup,
                ProducibleLookup = _producibleLookup,
                TransformLookup = _transformLookup,
                ObstacleLookup = _obstacleLookup,
                FactionLookup = _factionLookup,
                SiteLookup = _siteLookup,
            }.Schedule();
        }

        /// <summary>Single-threaded so several builders can repair one building.</summary>
        [BurstCompile]
        [WithNone(typeof(Dead))]
        [WithPresent(typeof(MoveDestination))]
        private partial struct RepairJob : IJobEntity
        {
            public float DeltaTime;
            public FactionRelations Relations;
            public ComponentLookup<Health> HealthLookup;
            [ReadOnly] public ComponentLookup<Producible> ProducibleLookup;
            [ReadOnly] public ComponentLookup<LocalTransform> TransformLookup;
            [ReadOnly] public ComponentLookup<NavObstacle> ObstacleLookup;
            [ReadOnly] public ComponentLookup<Faction> FactionLookup;
            [ReadOnly] public ComponentLookup<ConstructionProgress> SiteLookup;

            private void Execute(in Builder builder, ref ActiveOrder order, EnabledRefRW<ActiveOrder> busy,
                ref MoveDestination destination, EnabledRefRW<MoveDestination> moving, in LocalTransform transform,
                in NavAgent agent, in Faction faction)
            {
                if (!busy.ValueRO || order.Value.Type != OrderType.Repair)
                {
                    return;
                }

                var target = order.Value.Target;
                if (!RepairRules.NeedsRepair(HealthLookup, SiteLookup, FactionLookup, Relations, target, faction.Value) ||
                    !TransformLookup.HasComponent(target))
                {
                    busy.ValueRW = false;
                    moving.ValueRW = false;
                    return;
                }

                var position = TransformLookup[target].Position;
                if (!ReachMath.InReach(transform.Position, agent.Radius, position, ReachMath.HalfExtents(ObstacleLookup, target)))
                {
                    ReachMath.MoveTo(ref destination, moving, position);
                    return;
                }

                moving.ValueRW = false;
                ref var health = ref HealthLookup.GetRefRW(target).ValueRW;
                var buildTime = ProducibleLookup.TryGetComponent(target, out var producible) ? producible.BuildTime : 0f;
                health.Current = math.min(health.Max,
                    health.Current + DeltaTime * builder.Rate * health.Max / math.max(buildTime, 1f));
            }
        }
    }
}
