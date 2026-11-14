using HyperRTS.Simulation.Combat.Deploy;
using HyperRTS.Simulation.Combat.Weapons;
using HyperRTS.Simulation.Common.Combat;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Players;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Vision;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Combat.Targeting
{
    /// <summary>
    /// One unit's engagement: validates and leashes its <see cref="AttackTarget"/>, chases, backs off and halts. The
    /// engagement jobs split on whether the unit can move and share this.
    /// </summary>
    internal struct Engager
    {
        public TargetLookup Targets;
        public OwnerRelations Relations;
        [ReadOnly] public ComponentLookup<ActiveOrder> Orders;
        public OrderTraitTable Traits;
        [ReadOnly] public ComponentLookup<Deployable> Deployables;

        public void Engage(in Attacker unit, ref CombatStance stance, ref AttackTarget attack,
            EnabledRefRW<AttackTarget> attacking)
        {
            var order = CurrentOrder(unit.Entity);
            if (!attacking.ValueRO)
            {
                KeepPost(order, ref stance, unit);
                return;
            }

            // An ordered attack chases indefinitely; the stance only governs targets the unit picked itself.
            var target = attack.Value;
            var ordered = order.Type == OrderType.Attack && order.Target == target;
            if (Releases(unit, stance, ordered, ref attack, out var band))
            {
                Release(order, stance, ref attack, attacking, unit.Moves);
                return;
            }

            if (ordered)
            {
                stance.ReturnPoint = unit.Position;
            }

            // Bullets close in until the line is clear.
            var clear = Targets.CanFireAt(unit.Entity, unit.Position, unit.Mounts[0].Weapon, target);
            Approach(unit, target, ref attack, band, clear);
        }

        /// <summary>Attack-movers carry their post along, so a defensive leash is measured from where they engage.</summary>
        private void KeepPost(in Order order, ref CombatStance stance, in Attacker unit)
        {
            var standing = order.Type == OrderType.None && !unit.Moves.IsMoving;
            if (standing || Traits.Has(order.Type, OrderTraits.EngagesWhileMoving))
            {
                stance.ReturnPoint = unit.Position;
            }
        }

        /// <summary>
        /// Whether the unit lets go of its target: it is gone or out of reach, a target it picked itself is out of
        /// its firing arc or past the leash, or inside its minimum range. Otherwise sets the gap and range band.
        /// </summary>
        private bool Releases(in Attacker unit, in CombatStance stance, bool ordered, ref AttackTarget attack,
            out RangeBand band)
        {
            var entity = unit.Entity;
            var position = unit.Position;
            var primary = unit.Mounts[0];
            var weapon = primary.Weapon;
            var target = attack.Value;
            band = default;
            if (Loses(primary, unit.Owner, target, ordered))
            {
                return true;
            }

            // A target the unit chose itself and can no longer turn onto is dropped.
            if (!ordered && weapon.FiringArc > 0f && !Targets.InFiringArc(entity, weapon, target))
            {
                return true;
            }

            attack.Reach = Targets.Reach(entity, position, weapon, target);
            band = Reach(entity, position, unit.Mounts, target, attack.Reach);
            var gap = Targets.Gap(entity, position, target);
            var leash = stance.ScanRange(unit.Vision.Value, weapon.Range) * stance.LeashFactor;
            var strays = Roams(stance, unit.Moves) ? Strays(entity, position, stance, target, leash) : gap > band.Reach;
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
        private void Approach(in Attacker unit, Entity target, ref AttackTarget attack, in RangeBand band, bool clear)
        {
            var moves = unit.Moves;
            var middle = band.Middle;
            attack.BackingOff = BacksOff(attack, band.Floor, middle, moves);
            attack.InRange = !attack.BackingOff && band.Contains(attack.Gap) && clear;
            if (attack.BackingOff)
            {
                BackOff(unit.Entity, unit.Position, target, middle, moves);
            }
            else if (attack.Gap <= band.Reach && clear)
            {
                moves.Halt();
            }
            else
            {
                Chase(Targets.Position(target), moves);
            }
        }

        /// <summary>Gone, hidden, out of the weapon's reach or out of ammo.</summary>
        private bool Loses(in WeaponMount primary, in Owner owner, Entity target, bool ordered)
        {
            // Only orders point units at scenery, so an ordered target need only be attackable.
            var targets = primary.Weapon.Targets;
            var keep = ordered
                ? Targets.IsAttackable(target, owner.Value, Relations, targets)
                : Targets.IsValidTarget(target, owner.Value, Relations, targets);
            if (!keep)
            {
                return true;
            }

            return primary.Ammo.Empty;
        }

        /// <summary>Held and immobile units never move off, so only their reach limits them.</summary>
        private static bool Roams(in CombatStance stance, in UnitMove moves) =>
            stance.Value != Stance.HoldPosition && moves.IsMobile;

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

        private static void Release(in Order order, in CombatStance stance, ref AttackTarget attack,
            EnabledRefRW<AttackTarget> attacking, in UnitMove moves)
        {
            attack.InRange = false;
            attack.BackingOff = false;
            attacking.ValueRW = false;

            if (!moves.IsMobile)
            {
                return;
            }

            // Only idle defenders head home. Moves and escorts halt, and their order systems pick up again.
            if (order.Type == OrderType.None && stance.Value == Stance.Defensive)
            {
                moves.MoveTo(stance.ReturnPoint);
            }
            else
            {
                moves.Halt();
            }
        }

        private static void Chase(float3 targetPosition, in UnitMove moves)
        {
            if (moves.IsMobile)
            {
                moves.MoveTo(targetPosition, EngagementSystem.RepathDistance);
            }
        }

        /// <summary>Starts inside the minimum range if the unit can move, and keeps on until mid range or arrival.</summary>
        private static bool BacksOff(in AttackTarget attack, float floor, float middle, in UnitMove moves)
        {
            if (attack.BackingOff)
            {
                return attack.Gap < middle && moves.IsMoving;
            }

            return attack.Gap < floor && moves.IsMobile;
        }

        /// <summary>Retreats straight away from the target to <paramref name="gap"/> edge to edge.</summary>
        private void BackOff(Entity entity, float3 position, Entity target, float gap, in UnitMove moves)
        {
            moves.StandOff(Targets.Position(target), position, gap + Targets.Radius(entity) + Targets.Radius(target));
        }

        private Order CurrentOrder(Entity entity) => Orders.HasEnabled(entity) ? Orders[entity].Value : default;
    }
}
