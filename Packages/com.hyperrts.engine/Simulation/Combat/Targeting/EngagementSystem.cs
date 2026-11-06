using HyperRTS.Core;
using HyperRTS.Simulation.Combat.Weapons;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Vision;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Combat.Targeting
{
    /// <summary>
    /// Validates and leashes <see cref="AttackTarget"/>s, chases targets out of range, backs off inside minimum range
    /// and halts once in range. A rooted <see cref="Deployable"/> fights like a building.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(CombatSystemGroup))]
    [UpdateAfter(typeof(TargetAcquisitionSystem))]
    public partial struct EngagementSystem : ISystem
    {
        /// <summary>Chasers re-target their move only once the target has drifted this far from it.</summary>
        public const float RepathDistance = 1f;

        private TargetLookup _targets;
        private TargetQueries _targetQueries;
        private MoveLookup _moves;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _targets = new TargetLookup(ref state);
            _targetQueries = TargetQueries.Create(ref state);
            _moves = new MoveLookup(ref state);
            state.RequireForUpdate<FactionRelations>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _targets.Update(ref state, _targetQueries);
            _moves.Update(ref state);

            var traits = SystemAPI.GetSingleton<OrderTraitTable>();

            new EngageJob
            {
                Targets = _targets,
                Relations = SystemAPI.GetSingleton<FactionRelations>(),
                Orders = SystemAPI.GetComponentLookup<ActiveOrder>(true),
                Traits = traits,
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
            public OrderTraitTable Traits;
            public MoveLookup Moves;

            private void Execute(Entity entity, in LocalTransform transform, in DynamicBuffer<WeaponMount> mounts,
                in VisionRange vision, in Faction faction, ref CombatStance stance, ref AttackTarget attack,
                EnabledRefRW<AttackTarget> attacking)
            {
                var order = CurrentOrder(entity);
                if (!attacking.ValueRO)
                {
                    KeepPost(entity, transform.Position, order, ref stance);
                    return;
                }

                // An ordered attack chases indefinitely; the stance only governs targets the unit picked itself.
                var target = attack.Value;
                var ordered = order.Type == OrderType.Attack && order.Target == target;
                if (Releases(entity, transform.Position, mounts, vision, faction, stance, ordered, ref attack, out var band))
                {
                    Release(entity, order, stance, ref attack, attacking);
                    return;
                }

                if (ordered)
                {
                    stance.ReturnPoint = transform.Position;
                }

                // Bullets close in until the line is clear.
                var clear = Targets.CanFireAt(entity, transform.Position, mounts[0].Weapon, target);
                Engage(entity, transform.Position, target, ref attack, band, clear);
            }

            /// <summary>Attack-movers carry their post along, so a defensive leash is measured from where they engage.</summary>
            private void KeepPost(Entity entity, float3 position, in Order order, ref CombatStance stance)
            {
                var standing = order.Type == OrderType.None && !Moves.IsMoving(entity);
                if (standing || Traits.Has(order.Type, OrderTraits.EngagesWhileMoving))
                {
                    stance.ReturnPoint = position;
                }
            }

            /// <summary>
            /// Whether the unit lets go of its target: it is gone or out of reach, a target it picked itself is out of
            /// its firing arc or past the leash, or inside its minimum range. Otherwise sets the gap and range band.
            /// </summary>
            private bool Releases(Entity entity, float3 position, in DynamicBuffer<WeaponMount> mounts,
                in VisionRange vision, in Faction faction, in CombatStance stance, bool ordered, ref AttackTarget attack,
                out RangeBand band)
            {
                var primary = mounts[0];
                var weapon = primary.Weapon;
                var target = attack.Value;
                band = default;
                if (Loses(primary, faction, target, ordered))
                {
                    return true;
                }

                // A target the unit chose itself and can no longer turn onto is dropped.
                if (!ordered && weapon.FiringArc > 0f && !Targets.InFiringArc(entity, weapon, target))
                {
                    return true;
                }

                attack.Reach = Targets.Reach(entity, position, weapon, target);
                band = Reach(entity, position, mounts, target, attack.Reach);
                var gap = Targets.Gap(entity, position, target);
                var leash = stance.ScanRange(vision.Value, weapon.Range) * stance.LeashFactor;
                var strays = Roams(entity, stance) ? Strays(entity, position, stance, target, leash) : gap > band.Reach;
                if (!ordered && strays)
                {
                    return true;
                }

                attack.Gap = gap;
                return !ordered && gap < band.Floor;
            }

            /// <summary>
            /// Halts in range with a <paramref name="clear"/> line, chases otherwise, and backs an ordered unit off a
            /// target inside its minimum range.
            /// </summary>
            private void Engage(Entity entity, float3 position, Entity target, ref AttackTarget attack, in RangeBand band,
                bool clear)
            {
                var middle = band.Middle;
                attack.BackingOff = BacksOff(entity, attack, band.Floor, middle);
                attack.InRange = !attack.BackingOff && band.Contains(attack.Gap) && clear;
                if (attack.BackingOff)
                {
                    BackOff(entity, position, target, middle);
                }
                else if (attack.Gap <= band.Reach && clear)
                {
                    Moves.Halt(entity);
                }
                else
                {
                    Chase(entity, Targets.Position(target));
                }
            }

            /// <summary>Gone, hidden, out of the weapon's reach or out of ammo.</summary>
            private bool Loses(in WeaponMount primary, in Faction faction, Entity target, bool ordered)
            {
                // Only orders point units at scenery, so an ordered target need only be attackable.
                var targets = primary.Weapon.Targets;
                var keep = ordered
                    ? Targets.IsAttackable(target, faction.Value, Relations, targets)
                    : Targets.IsValidTarget(target, faction.Value, Relations, targets);
                if (!keep)
                {
                    return true;
                }

                return primary.Ammo.Empty;
            }

            /// <summary>Held and immobile units never move off, so only their reach limits them.</summary>
            private bool Roams(Entity entity, in CombatStance stance) =>
                stance.Value != Stance.HoldPosition && Moves.IsMobile(entity);

            private bool Strays(Entity entity, float3 position, in CombatStance stance, Entity target, float leash)
            {
                // Defensive units measure from their post, so a fleeing target can't drag them away.
                var origin = stance.Value == Stance.Defensive ? stance.ReturnPoint : position;
                return Targets.Gap(entity, origin, target) > leash;
            }

            /// <summary>Longest range and shortest minimum of the primary and the ready shared-target weapons that can hit it.</summary>
            private RangeBand Reach(Entity entity, float3 position, in DynamicBuffer<WeaponMount> mounts, Entity target,
                float reach)
            {
                var band = mounts[0].Weapon.Band(reach);
                WeaponMount.ExtendReach(mounts, Targets, entity, position, target, ref band);
                return band;
            }

            private void Release(Entity entity, in Order order, in CombatStance stance, ref AttackTarget attack,
                EnabledRefRW<AttackTarget> attacking)
            {
                attack.InRange = false;
                attack.BackingOff = false;
                attacking.ValueRW = false;

                if (!Moves.IsMobile(entity))
                {
                    return;
                }

                // Only idle defenders head home. Moves and escorts halt, and their order systems pick up again.
                if (order.Type == OrderType.None && stance.Value == Stance.Defensive)
                {
                    Moves.MoveTo(entity, stance.ReturnPoint);
                }
                else
                {
                    Moves.Halt(entity);
                }
            }

            private void Chase(Entity entity, float3 targetPosition)
            {
                if (Moves.IsMobile(entity))
                {
                    Moves.MoveTo(entity, targetPosition, RepathDistance);
                }
            }

            /// <summary>Starts inside the minimum range if the unit can move, and keeps on until mid range or arrival.</summary>
            private bool BacksOff(Entity entity, in AttackTarget attack, float floor, float middle)
            {
                if (attack.BackingOff)
                {
                    return attack.Gap < middle && Moves.IsMoving(entity);
                }

                return attack.Gap < floor && Moves.IsMobile(entity);
            }

            /// <summary>Retreats straight away from the target to <paramref name="gap"/> edge to edge.</summary>
            private void BackOff(Entity entity, float3 position, Entity target, float gap)
            {
                var from = Targets.Position(target);
                var level = new float3(position.x - from.x, 0f, position.z - from.z);
                var away = math.normalizesafe(level, new float3(0f, 0f, -1f));
                var goal = from + away * (gap + Targets.Radius(entity) + Targets.Radius(target));
                Moves.MoveTo(entity, goal, RepathDistance);
            }

            private Order CurrentOrder(Entity entity) => Orders.HasEnabled(entity) ? Orders[entity].Value : default;
        }
    }
}
