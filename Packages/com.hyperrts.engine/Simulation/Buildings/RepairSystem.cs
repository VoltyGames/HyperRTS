using HyperRTS.Core;
using HyperRTS.Simulation.Combat;
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
    /// Runs Repair orders: builders walk to an allied finished building and restore health as fast as they would
    /// build it (<c>Rate × MaxHealth / BuildTime</c> per second). The order ends once it is whole or gone.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(ProductionSystemGroup))]
    public partial struct RepairSystem : ISystem
    {
        /// <summary>Floor on the full-repair time, so instant-build and unproduced structures don't heal in a frame.</summary>
        private const float MinRepairTime = 1f;

        private DamageWriter _damage;
        private ComponentLookup<Health> _healthLookup;
        private ComponentLookup<Producible> _producibleLookup;
        private ComponentLookup<LocalTransform> _transformLookup;
        private ComponentLookup<NavObstacle> _obstacleLookup;
        private ComponentLookup<Faction> _factionLookup;
        private ComponentLookup<ConstructionProgress> _siteLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _damage = new DamageWriter(ref state);
            _healthLookup = state.GetComponentLookup<Health>(true);
            _producibleLookup = state.GetComponentLookup<Producible>(true);
            _transformLookup = state.GetComponentLookup<LocalTransform>(true);
            _obstacleLookup = state.GetComponentLookup<NavObstacle>(true);
            _factionLookup = state.GetComponentLookup<Faction>(true);
            _siteLookup = state.GetComponentLookup<ConstructionProgress>(true);
            state.RequireForUpdate<FactionRelations>();
            state.RequireForUpdate<DamageQueue>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _damage.Update(ref state, SystemAPI.GetSingletonEntity<DamageQueue>());
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
                Damage = _damage,
                HealthLookup = _healthLookup,
                ProducibleLookup = _producibleLookup,
                TransformLookup = _transformLookup,
                ObstacleLookup = _obstacleLookup,
                FactionLookup = _factionLookup,
                SiteLookup = _siteLookup,
            }.Schedule();
        }

        /// <summary>Single-threaded: every heal appends to the one damage queue.</summary>
        [BurstCompile]
        [WithNone(typeof(Dead))]
        [WithPresent(typeof(MoveDestination))]
        private partial struct RepairJob : IJobEntity
        {
            public float DeltaTime;
            public FactionRelations Relations;
            public DamageWriter Damage;
            [ReadOnly] public ComponentLookup<Health> HealthLookup;
            [ReadOnly] public ComponentLookup<Producible> ProducibleLookup;
            [ReadOnly] public ComponentLookup<LocalTransform> TransformLookup;
            [ReadOnly] public ComponentLookup<NavObstacle> ObstacleLookup;
            [ReadOnly] public ComponentLookup<Faction> FactionLookup;
            [ReadOnly] public ComponentLookup<ConstructionProgress> SiteLookup;

            private void Execute(Entity entity, in Builder builder, ref ActiveOrder order, EnabledRefRW<ActiveOrder> busy,
                ref MoveDestination destination, EnabledRefRW<MoveDestination> moving, in LocalTransform transform,
                in NavAgent agent, in Faction faction)
            {
                if (!busy.ValueRO || order.Value.Type != OrderType.Repair)
                {
                    return;
                }

                var target = order.Value.Target;
                if (!CanRepair(target, faction.Value))
                {
                    ActiveOrder.Finish(busy, moving);
                    return;
                }

                var position = TransformLookup[target].Position;
                var extents = ReachMath.HalfExtents(ObstacleLookup, target);
                if (!ReachMath.Approach(ref destination, moving, transform.Position, agent.Radius, position, extents))
                {
                    return;
                }

                var buildTime = Producible.BuildTimeOf(ProducibleLookup, target, MinRepairTime);
                var perSecond = builder.Rate * HealthLookup[target].Max / buildTime;
                Damage.Add(new DamageEvent
                {
                    Target = target,
                    Position = position,
                    Source = entity,
                    SourceFaction = faction.Value,
                    Amount = -DeltaTime * perSecond,
                });
            }

            private bool CanRepair(Entity target, byte faction)
            {
                if (!TransformLookup.HasComponent(target))
                {
                    return false;
                }

                return BuildingRules.NeedsRepair(HealthLookup, SiteLookup, FactionLookup, Relations, target, faction);
            }
        }
    }
}
