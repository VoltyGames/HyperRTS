using HyperRTS.Simulation.Combat.Damage;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Events;
using HyperRTS.Simulation.Common.Players;
using HyperRTS.Simulation.Destruction;
using HyperRTS.Simulation.Navigation.Agents;
using HyperRTS.Simulation.Navigation.Grid;
using HyperRTS.Simulation.Navigation.Reach;
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

        private EventWriter<DamageEvent> _damage;
        private TargetReach _reach;
        private RepairLookup _repairs;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _damage = new EventWriter<DamageEvent>(ref state);
            _reach = new TargetReach(ref state);
            _repairs = new RepairLookup(ref state);
            state.RequireForUpdate<MapSettings>();
            state.RequireForUpdate<NavGrid>();
            state.RequireForUpdate<OwnerRelations>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _damage.Update(ref state);
            _repairs.Update(ref state);
            _reach.Update(ref state, SystemAPI.GetSingleton<MapSettings>());

            new RepairJob
            {
                DeltaTime = SystemAPI.Time.DeltaTime,
                Relations = SystemAPI.GetSingleton<OwnerRelations>(),
                Damage = _damage,
                HealthLookup = SystemAPI.GetComponentLookup<Health>(true),
                ProducibleLookup = SystemAPI.GetComponentLookup<Producible>(true),
                Reach = _reach,
                RepairableLookup = SystemAPI.GetComponentLookup<Repairable>(true),
                Repairs = _repairs,
                SectionLookup = SystemAPI.GetBufferLookup<Section>(true),
                Grid = SystemAPI.GetSingleton<NavGrid>(),
            }.Schedule();
        }

        /// <summary>Single-threaded: every heal appends to the one damage queue.</summary>
        [BurstCompile]
        [WithNone(typeof(Dead))]
        [WithPresent(typeof(MoveDestination), typeof(Arrival))]
        private partial struct RepairJob : IJobEntity
        {
            public float DeltaTime;
            public OwnerRelations Relations;
            public EventWriter<DamageEvent> Damage;
            [ReadOnly] public ComponentLookup<Health> HealthLookup;
            [ReadOnly] public ComponentLookup<Producible> ProducibleLookup;
            public TargetReach Reach;
            [ReadOnly] public NavGrid Grid;
            public RepairLookup Repairs;
            [ReadOnly] public ComponentLookup<Repairable> RepairableLookup;
            [ReadOnly] public BufferLookup<Section> SectionLookup;

            private void Execute(Entity entity, in Builder builder, ref ActiveOrder order, EnabledRefRW<ActiveOrder> busy,
                RefRW<MoveDestination> destination, EnabledRefRW<MoveDestination> moving, EnabledRefRW<Arrival> arrived,
                in LocalTransform transform, in NavAgent agent, in Owner owner)
            {
                if (!busy.ValueRO || order.Value.Type != OrderType.Repair)
                {
                    return;
                }

                var target = order.Value.Target;
                if (!CanRepair(target, owner.Value))
                {
                    ActiveOrder.Finish(busy, moving);
                    return;
                }

                var position = Reach.Position(target);
                var walk = Reach.Approach(target, transform, agent,
                    new MoveControl(destination, moving, arrived), Grid);
                if (ActiveOrder.StopsShort(walk, busy, moving))
                {
                    return;
                }

                var perSecond = PerSecond(target, builder);
                Damage.Add(DamageEvent.Direct(target, position, entity, owner.Value, -DeltaTime * perSecond));
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

            private bool CanRepair(Entity target, byte owner)
            {
                if (!Reach.Exists(target))
                {
                    return false;
                }

                return Repairs.NeedsRepair(target, owner, Relations);
            }
        }
    }
}
