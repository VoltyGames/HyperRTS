using HyperRTS.Core;
using HyperRTS.Simulation.Combat.Weapons;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Navigation.Spatial;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Vision;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;

namespace HyperRTS.Simulation.Combat.Targeting
{
    /// <summary>
    /// Points idle, attack-moving and hold-position weapons (and finished towers) at the nearest hostile in range that
    /// the weapon can hit and its team can see: in the fog's sight and not hidden by stealth, so long-range fires need
    /// a spotter. Units on any other order, or out of ammo, never auto-acquire.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(CombatSystemGroup))]
    [UpdateAfter(typeof(AttackOrderSystem))]
    public partial struct TargetAcquisitionSystem : ISystem
    {
        private TargetLookup _targets;
        private EntityQuery _fogQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _targets = new TargetLookup(ref state);
            _fogQuery = TargetLookup.FogQuery(ref state);
            state.RequireForUpdate<SpatialIndex>();
            state.RequireForUpdate<FactionRelations>();
            state.RequireForUpdate<FogOfWar>();
            state.RequireForUpdate<SimulationFrame>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _targets.Update(ref state, _fogQuery);

            var traits = SystemAPI.GetSingleton<OrderTraitTable>();

            new AcquireJob
            {
                Index = SystemAPI.GetSingleton<SpatialIndex>(),
                Relations = SystemAPI.GetSingleton<FactionRelations>(),
                Targets = _targets,
                Orders = SystemAPI.GetComponentLookup<ActiveOrder>(true),
                Traits = traits,
                Slot = new ScanSlot(SystemAPI.GetSingleton<SimulationFrame>()),
            }.ScheduleParallel();
        }

        [BurstCompile]
        [WithNone(typeof(Offline))]
        [WithPresent(typeof(AttackTarget))]
        private partial struct AcquireJob : IJobEntity
        {
            [ReadOnly] public SpatialIndex Index;
            public FactionRelations Relations;
            public TargetLookup Targets;
            [ReadOnly] public ComponentLookup<ActiveOrder> Orders;
            public OrderTraitTable Traits;
            public ScanSlot Slot;

            private void Execute(Entity entity, in LocalTransform transform, in DynamicBuffer<WeaponMount> mounts,
                in CombatStance stance, in VisionRange vision, in Faction faction, ref AttackTarget attack,
                EnabledRefRW<AttackTarget> attacking)
            {
                if (!Slot.Owns(entity) || attacking.ValueRO || stance.Value == Stance.Passive)
                {
                    return;
                }

                var primary = mounts[0];
                if (primary.Ammo.Empty || !MayAutoAcquire(entity))
                {
                    return;
                }

                var weapon = primary.Weapon;
                var holding = stance.Value == Stance.HoldPosition;
                var range = holding ? Targets.ScanReach(entity, weapon) : weapon.ScanRange(vision.Value);
                var radius = Targets.Radius(entity);
                var finder = new NearestHostile(Relations, Targets, weapon, faction.Value, entity, transform.Position,
                    radius, holding);
                Index.Query(transform.Position, range + radius, ref finder);

                if (finder.Best != Entity.Null)
                {
                    attack = new AttackTarget { Value = finder.Best };
                    attacking.ValueRW = true;
                }
            }

            private bool MayAutoAcquire(Entity entity)
            {
                if (!Orders.HasEnabled(entity))
                {
                    return true;
                }

                return Traits.Has(Orders[entity].Value.Type, OrderTraits.EngagesWhileMoving);
            }
        }
    }
}
