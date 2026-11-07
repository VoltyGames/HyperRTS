using HyperRTS.Simulation.Combat.Weapons;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Players;
using HyperRTS.Simulation.Navigation.Spatial;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Vision;
using HyperRTS.Simulation.Vision.Fog;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;

namespace HyperRTS.Simulation.Combat.Targeting
{
    /// <summary>
    /// Points idle, attack-moving and hold-position weapons at the nearest hostile they can hit and the team can see,
    /// so long-range fires need a spotter.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(CombatSystemGroup))]
    [UpdateAfter(typeof(AttackOrderSystem))]
    public partial struct TargetAcquisitionSystem : ISystem
    {
        private TargetLookup _targets;
        private TargetQueries _targetQueries;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _targets = new TargetLookup(ref state);
            _targetQueries = TargetQueries.Create(ref state);
            state.RequireForUpdate<SpatialIndex>();
            state.RequireForUpdate<OwnerRelations>();
            state.RequireForUpdate<FogOfWar>();
            state.RequireForUpdate<SimulationFrame>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _targets.Update(ref state, _targetQueries);

            var traits = SystemAPI.GetSingleton<OrderTraitTable>();

            new AcquireJob
            {
                Index = SystemAPI.GetSingleton<SpatialIndex>(),
                Relations = SystemAPI.GetSingleton<OwnerRelations>(),
                Targets = _targets,
                Orders = SystemAPI.GetComponentLookup<ActiveOrder>(true),
                Traits = traits,
                Scans = new ScanSchedule(SystemAPI.GetSingleton<SimulationFrame>()),
            }.ScheduleParallel();
        }

        [BurstCompile]
        [WithNone(typeof(Offline))]
        [WithPresent(typeof(AttackTarget))]
        private partial struct AcquireJob : IJobEntity
        {
            [ReadOnly] public SpatialIndex Index;
            public OwnerRelations Relations;
            public TargetLookup Targets;
            [ReadOnly] public ComponentLookup<ActiveOrder> Orders;
            public OrderTraitTable Traits;
            public ScanSchedule Scans;

            private void Execute(Entity entity, in LocalTransform transform, in DynamicBuffer<WeaponMount> mounts,
                in CombatStance stance, in VisionRange vision, in Owner owner, ref AttackTarget attack,
                EnabledRefRW<AttackTarget> attacking)
            {
                if (!Scans.IsDue(entity) || attacking.ValueRO || stance.Value == Stance.Passive)
                {
                    return;
                }

                var primary = mounts[0];
                if (primary.Weapon.ShootsProjectiles || primary.Ammo.Empty || !MayAutoAcquire(entity))
                {
                    return;
                }

                var weapon = primary.Weapon;
                var holding = stance.Value == Stance.HoldPosition;
                var range = holding ? Targets.ScanReach(entity, weapon) : stance.ScanRange(vision.Value, weapon.Range);
                var finder = new NearestHostile(Relations, Targets, weapon, owner.Value, entity, transform.Position);
                finder.Search(Index, range, holding);

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
