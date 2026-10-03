using HyperRTS.Core;
using HyperRTS.Simulation.Buildings;
using HyperRTS.Simulation.Match;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Spatial;
using HyperRTS.Simulation.Vision;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Combat
{
    /// <summary>
    /// Points idle, attack-moving and hold-position weapons (and finished towers) at the nearest hostile in range.
    /// Units on any other order never auto-acquire.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(CombatSystemGroup))]
    [UpdateAfter(typeof(AttackOrderSystem))]
    public partial struct TargetAcquisitionSystem : ISystem
    {
        /// <summary>Each weapon scans every Nth frame, spreading the spatial queries over frames.</summary>
        public const int ScanInterval = 4;

        private TargetLookup _targets;
        private ComponentLookup<ActiveOrder> _orders;
        private uint _frame;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _targets = new TargetLookup(ref state);
            _orders = state.GetComponentLookup<ActiveOrder>(true);
            state.RequireForUpdate<SpatialIndex>();
            state.RequireForUpdate<FactionRelations>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _targets.Update(ref state);
            _orders.Update(ref state);
            _frame++;

            new AcquireJob
            {
                Index = SystemAPI.GetSingleton<SpatialIndex>(),
                Relations = SystemAPI.GetSingleton<FactionRelations>(),
                Targets = _targets,
                Orders = _orders,
                Slot = (int)(_frame % ScanInterval),
            }.ScheduleParallel();
        }

        [BurstCompile]
        [WithNone(typeof(ConstructionProgress))]
        [WithPresent(typeof(AttackTarget))]
        private partial struct AcquireJob : IJobEntity
        {
            [ReadOnly] public SpatialIndex Index;
            public FactionRelations Relations;
            public TargetLookup Targets;
            [ReadOnly] public ComponentLookup<ActiveOrder> Orders;
            public int Slot;

            private void Execute(Entity entity, in LocalTransform transform, in Weapon weapon, in CombatStance stance,
                in VisionRange vision, in Faction faction, ref AttackTarget attack, EnabledRefRW<AttackTarget> attacking)
            {
                if (attacking.ValueRO || stance.Value == Stance.Passive || entity.Index % ScanInterval != Slot ||
                    !MayAutoAcquire(entity))
                {
                    return;
                }

                var range = stance.Value == Stance.HoldPosition
                    ? weapon.Range
                    : CombatMath.AcquireRange(weapon, vision.Value);
                var finder = new NearestHostile
                {
                    Relations = Relations,
                    Faction = faction.Value,
                    Center = transform.Position,
                    BestDistance = float.MaxValue,
                };
                Index.Query(transform.Position, range + Targets.Radius(entity), ref finder);

                if (finder.Best != Entity.Null)
                {
                    attack = new AttackTarget { Value = finder.Best };
                    attacking.ValueRW = true;
                }
            }

            private bool MayAutoAcquire(Entity entity) =>
                !Orders.HasComponent(entity) || !Orders.IsComponentEnabled(entity) ||
                Orders[entity].Value.Type == OrderType.AttackMove;
        }

        private struct NearestHostile : ISpatialVisitor
        {
            public FactionRelations Relations;
            public byte Faction;
            public float3 Center;
            public Entity Best;
            public float BestDistance;

            public void Visit(in SpatialEntry entry)
            {
                if (!Relations.IsHostile(Faction, entry.Faction))
                {
                    return;
                }

                // Index order varies run to run, so ties go to the lower entity index for determinism.
                var distance = math.distance(entry.Position.xz, Center.xz) - entry.Radius;
                if (distance < BestDistance || (distance == BestDistance && entry.Entity.Index < Best.Index))
                {
                    Best = entry.Entity;
                    BestDistance = distance;
                }
            }
        }
    }
}
