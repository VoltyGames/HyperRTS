using HyperRTS.Core;
using HyperRTS.Simulation.Match;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Units;
using HyperRTS.Simulation.Vision;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Combat
{
    /// <summary>
    /// Validates and leashes <see cref="AttackTarget"/>s, chases targets out of weapon range and halts once in range.
    /// Also keeps <see cref="CombatStance.Anchor"/> on idle, unengaged units.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(CombatSystemGroup))]
    [UpdateAfter(typeof(TargetAcquisitionSystem))]
    public partial struct EngagementSystem : ISystem
    {
        /// <summary>Chasers re-target their move only once the target has drifted this far from it.</summary>
        public const float RepathDistance = 1f;

        /// <summary>Auto-acquired targets are dropped beyond this multiple of the acquire range.</summary>
        public const float LeashFactor = 1.5f;

        private TargetLookup _targets;
        private ComponentLookup<ActiveOrder> _orders;
        private ComponentLookup<MoveDestination> _moves;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _targets = new TargetLookup(ref state);
            _orders = state.GetComponentLookup<ActiveOrder>(true);
            _moves = state.GetComponentLookup<MoveDestination>();
            state.RequireForUpdate<FactionRelations>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _targets.Update(ref state);
            _orders.Update(ref state);
            _moves.Update(ref state);

            new EngageJob
            {
                Targets = _targets,
                Relations = SystemAPI.GetSingleton<FactionRelations>(),
                Orders = _orders,
                Moves = _moves,
            }.ScheduleParallel();
        }

        [BurstCompile]
        [WithPresent(typeof(AttackTarget))]
        private partial struct EngageJob : IJobEntity
        {
            public TargetLookup Targets;
            public FactionRelations Relations;
            [ReadOnly] public ComponentLookup<ActiveOrder> Orders;

            // Each entity writes only its own MoveDestination.
            [NativeDisableParallelForRestriction] public ComponentLookup<MoveDestination> Moves;

            private void Execute(Entity entity, in LocalTransform transform, in Weapon weapon, in VisionRange vision,
                in Faction faction, ref CombatStance stance, ref AttackTarget attack, EnabledRefRW<AttackTarget> attacking)
            {
                var order = CurrentOrder(entity);
                if (!attacking.ValueRO)
                {
                    // Attack-movers carry their post along, so a defensive leash is measured from where they engage.
                    if ((order.Type == OrderType.None && !IsMoving(entity)) || order.Type.EngagesWhileMoving())
                    {
                        stance.Anchor = transform.Position;
                    }

                    return;
                }

                // An ordered attack chases indefinitely; the stance only governs targets the unit picked itself.
                var target = attack.Value;
                var ordered = order.Type == OrderType.Attack && order.Target == target;
                if (!Targets.IsValidTarget(target, faction.Value, Relations) ||
                    (!ordered && ShouldLeash(entity, transform.Position, weapon, vision, stance, target)))
                {
                    Release(entity, order, stance, ref attack, attacking);
                    return;
                }

                if (ordered)
                {
                    stance.Anchor = transform.Position;
                }

                attack.InRange = Distance(entity, transform.Position, target) <= weapon.Range;
                if (attack.InRange)
                {
                    Halt(entity);
                }
                else
                {
                    Chase(entity, Targets.Position(target));
                }
            }

            private bool ShouldLeash(Entity entity, float3 position, in Weapon weapon, in VisionRange vision,
                in CombatStance stance, Entity target)
            {
                if (stance.Value == Stance.HoldPosition || !Moves.HasComponent(entity))
                {
                    return Distance(entity, position, target) > weapon.Range;
                }

                // Defensive units measure from their post, so a fleeing target can't drag them away.
                var origin = stance.Value == Stance.Defensive ? stance.Anchor : position;
                return Distance(entity, origin, target) > CombatMath.AcquireRange(weapon, vision.Value) * LeashFactor;
            }

            private float Distance(Entity entity, float3 position, Entity target) =>
                CombatMath.EdgeDistance(position, Targets.Radius(entity), Targets.Position(target), Targets.Radius(target));

            private void Release(Entity entity, in Order order, in CombatStance stance, ref AttackTarget attack,
                EnabledRefRW<AttackTarget> attacking)
            {
                attack.InRange = false;
                attacking.ValueRW = false;

                if (!Moves.HasComponent(entity))
                {
                    return;
                }

                // Attack-move heads straight back to its goal: a disabled destination would read as arrival to
                // MoveOrderSystem if it never saw the engagement. Only idle defenders head home.
                if (order.Type.EngagesWhileMoving())
                {
                    MoveTo(entity, order.Position);
                }
                else if (order.Type == OrderType.None && stance.Value == Stance.Defensive)
                {
                    MoveTo(entity, stance.Anchor);
                }
                else
                {
                    Halt(entity);
                }
            }

            private void Chase(Entity entity, float3 targetPosition)
            {
                if (!Moves.HasComponent(entity))
                {
                    return;
                }

                var current = Moves[entity].Value;
                if (!Moves.IsComponentEnabled(entity) ||
                    math.distancesq(current.xz, targetPosition.xz) > RepathDistance * RepathDistance)
                {
                    MoveTo(entity, targetPosition);
                }
            }

            private void Halt(Entity entity)
            {
                if (Moves.HasComponent(entity))
                {
                    Moves.SetComponentEnabled(entity, false);
                }
            }

            private void MoveTo(Entity entity, float3 destination)
            {
                Moves[entity] = new MoveDestination { Value = destination };
                Moves.SetComponentEnabled(entity, true);
            }

            private bool IsMoving(Entity entity) => Moves.HasComponent(entity) && Moves.IsComponentEnabled(entity);

            private Order CurrentOrder(Entity entity) =>
                Orders.HasComponent(entity) && Orders.IsComponentEnabled(entity) ? Orders[entity].Value : default;
        }
    }
}
