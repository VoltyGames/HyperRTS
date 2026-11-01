using HyperRTS.Core;
using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Destruction;
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
        private TargetReach _reach;
        private ComponentLookup<Faction> _factionLookup;
        private ComponentLookup<ConstructionProgress> _siteLookup;
        private ComponentLookup<Repairable> _repairableLookup;
        private BufferLookup<Section> _sectionLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _damage = new DamageWriter(ref state);
            _healthLookup = state.GetComponentLookup<Health>(true);
            _producibleLookup = state.GetComponentLookup<Producible>(true);
            _reach = new TargetReach(ref state);
            state.RequireForUpdate<MapSettings>();
            _factionLookup = state.GetComponentLookup<Faction>(true);
            _siteLookup = state.GetComponentLookup<ConstructionProgress>(true);
            _repairableLookup = state.GetComponentLookup<Repairable>(true);
            _sectionLookup = state.GetBufferLookup<Section>(true);
            state.RequireForUpdate<FactionRelations>();
            state.RequireForUpdate<DamageQueue>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _damage.Update(ref state, SystemAPI.GetSingletonEntity<DamageQueue>());
            _healthLookup.Update(ref state);
            _producibleLookup.Update(ref state);
            _reach.Update(ref state, SystemAPI.GetSingleton<MapSettings>());
            _factionLookup.Update(ref state);
            _siteLookup.Update(ref state);
            _repairableLookup.Update(ref state);
            _sectionLookup.Update(ref state);

            new RepairJob
            {
                DeltaTime = SystemAPI.Time.DeltaTime,
                Relations = SystemAPI.GetSingleton<FactionRelations>(),
                Damage = _damage,
                HealthLookup = _healthLookup,
                ProducibleLookup = _producibleLookup,
                Reach = _reach,
                FactionLookup = _factionLookup,
                SiteLookup = _siteLookup,
                RepairableLookup = _repairableLookup,
                SectionLookup = _sectionLookup,
            }.Schedule();
        }

        /// <summary>Single-threaded: every heal appends to the one damage queue.</summary>
        [BurstCompile]
        [WithNone(typeof(Dead))]
        [WithPresent(typeof(MoveDestination), typeof(Arrival))]
        private partial struct RepairJob : IJobEntity
        {
            public float DeltaTime;
            public FactionRelations Relations;
            public DamageWriter Damage;
            [ReadOnly] public ComponentLookup<Health> HealthLookup;
            [ReadOnly] public ComponentLookup<Producible> ProducibleLookup;
            public TargetReach Reach;
            [ReadOnly] public ComponentLookup<Faction> FactionLookup;
            [ReadOnly] public ComponentLookup<ConstructionProgress> SiteLookup;
            [ReadOnly] public ComponentLookup<Repairable> RepairableLookup;
            [ReadOnly] public BufferLookup<Section> SectionLookup;

            private void Execute(Entity entity, in Builder builder, ref ActiveOrder order, EnabledRefRW<ActiveOrder> busy,
                RefRW<MoveDestination> destination, EnabledRefRW<MoveDestination> moving, EnabledRefRW<Arrival> arrived,
                in LocalTransform transform, in NavAgent agent, in Faction faction)
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

                var position = Reach.Position(target);
                var walk = Reach.Approach(target, transform, agent, new MoveControl(destination, moving, arrived));
                if (ActiveOrder.StopsShort(walk, busy, moving))
                {
                    return;
                }

                var perSecond = PerSecond(target, builder);
                Damage.Add(new DamageEvent
                {
                    Target = target,
                    Position = position,
                    Source = entity,
                    SourceFaction = faction.Value,
                    Amount = -DeltaTime * perSecond,
                });
            }

            /// <summary>A repairable building takes a fixed time per section; any other as long as it took to build.</summary>
            private float PerSecond(Entity target, in Builder builder)
            {
                if (RepairableLookup.TryGetComponent(target, out var repairable))
                {
                    return repairable.PerSecond(builder.Rate, SectionLookup[target]);
                }

                var buildTime = Producible.GetBuildTime(ProducibleLookup, target, MinRepairTime);
                return builder.Rate * HealthLookup[target].Max / buildTime;
            }

            private bool CanRepair(Entity target, byte faction)
            {
                if (!Reach.Exists(target))
                {
                    return false;
                }

                return BuildingRules.NeedsRepair(HealthLookup, SiteLookup, FactionLookup, RepairableLookup,
                    Relations, target, faction);
            }
        }
    }
}
