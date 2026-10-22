using HyperRTS.Core;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Navigation;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Vision;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;

namespace HyperRTS.Simulation.Combat
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
        /// <summary>Each weapon scans every Nth frame, spreading the spatial queries over frames.</summary>
        public const int ScanInterval = 4;

        private TargetLookup _targets;
        private EntityQuery _fogQuery;
        private ComponentLookup<ActiveOrder> _orders;
        private ComponentLookup<Ammo> _ammo;
        private BufferLookup<ArmorModifier> _armor;
        private uint _frame;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _targets = new TargetLookup(ref state);
            _fogQuery = TargetLookup.FogQuery(ref state);
            _orders = state.GetComponentLookup<ActiveOrder>(true);
            _ammo = state.GetComponentLookup<Ammo>(true);
            _armor = state.GetBufferLookup<ArmorModifier>(true);
            state.RequireForUpdate<SpatialIndex>();
            state.RequireForUpdate<FactionRelations>();
            state.RequireForUpdate<FogOfWar>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _targets.Update(ref state, _fogQuery);
            _orders.Update(ref state);
            _ammo.Update(ref state);
            _armor.Update(ref state);
            _frame++;

            var traits = SystemAPI.GetSingleton<OrderTraitTable>();

            new AcquireJob
            {
                Index = SystemAPI.GetSingleton<SpatialIndex>(),
                Relations = SystemAPI.GetSingleton<FactionRelations>(),
                Targets = _targets,
                Orders = _orders,
                Traits = traits,
                AmmoLookup = _ammo,
                Armor = _armor,
                Slot = (int)(_frame % ScanInterval),
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
            [ReadOnly] public ComponentLookup<Ammo> AmmoLookup;
            [ReadOnly] public BufferLookup<ArmorModifier> Armor;
            public int Slot;

            private void Execute(Entity entity, in LocalTransform transform, in Weapon weapon, in CombatStance stance,
                in VisionRange vision, in Faction faction, ref AttackTarget attack, EnabledRefRW<AttackTarget> attacking)
            {
                var scanning = entity.Index % ScanInterval == Slot;
                if (!scanning || attacking.ValueRO || stance.Value == Stance.Passive)
                {
                    return;
                }

                if (!MayAutoAcquire(entity))
                {
                    return;
                }

                var range = stance.Value == Stance.HoldPosition
                    ? weapon.Range
                    : CombatMath.AcquireRange(weapon, vision.Value);
                var radius = Targets.Radius(entity);
                var finder = new NearestHostile(Relations, Targets, Armor, weapon, faction.Value, transform.Position,
                    radius);
                Index.Query(transform.Position, range + radius, ref finder);

                if (finder.Best != Entity.Null)
                {
                    attack = new AttackTarget { Value = finder.Best };
                    attacking.ValueRW = true;
                }
            }

            private bool MayAutoAcquire(Entity entity)
            {
                if (Ammo.IsEmpty(AmmoLookup, entity))
                {
                    return false;
                }

                if (!Orders.HasEnabled(entity))
                {
                    return true;
                }

                return Traits.Has(Orders[entity].Value.Type, OrderTraits.EngagesWhileMoving);
            }
        }
    }
}
