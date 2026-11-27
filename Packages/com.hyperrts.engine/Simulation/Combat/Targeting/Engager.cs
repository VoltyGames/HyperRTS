using HyperRTS.Simulation.Combat.Deploy;
using HyperRTS.Simulation.Combat.Weapons;
using HyperRTS.Simulation.Common.Combat;
using HyperRTS.Simulation.Common.Players;
using HyperRTS.Simulation.Orders;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Combat.Targeting;

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
        var order = ActiveOrder.Current(Orders, unit.Entity);
        if (!attacking.ValueRO)
        {
            KeepPost(order, ref stance, unit);
            return;
        }

        // An ordered attack chases indefinitely; the stance only governs targets the unit picked itself.
        var target = attack.Value;
        var ordered = order.Type == OrderType.Attack && order.Target == target;
        var primary = unit.Mounts.Primary;
        if (!Targets.KeepsAttacking(target, unit.Owner.Value, Relations, primary, ordered))
        {
            Release(order, stance, ref attack, attacking, unit.Moves);
            return;
        }

        var range = Measure(unit, target);
        attack.Reach = range.Reach;
        attack.Gap = range.Gap;
        if (!ordered && Releases(unit, stance, target, range))
        {
            Release(order, stance, ref attack, attacking, unit.Moves);
            return;
        }

        if (ordered)
        {
            stance.ReturnPoint = unit.Position;
        }

        // Direct fire closes in until its line is clear: bullets of everything, rounds of the ground.
        var clear = Targets.CanFireAt(unit.Entity, unit.Position, primary.Weapon, target);
        Approach(unit, target, ref attack, range.Band, clear);
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

    /// <summary>The gap to the target, the primary's reach and the band of every weapon that joins the attack.</summary>
    private AttackRange Measure(in Attacker unit, Entity target)
    {
        var entity = unit.Entity;
        var position = unit.Position;
        var reach = Targets.Reach(entity, position, unit.Mounts.Primary.Weapon, target);
        var band = unit.Mounts.Primary.Weapon.Band(reach);
        ExtendReach(unit, target, ref band);
        return new AttackRange { Gap = Targets.Gap(entity, position, target), Reach = reach, Band = band };
    }

    /// <summary>
    /// Whether the unit lets go of a target it picked itself: out of its firing arc, past the leash or its reach,
    /// or inside its minimum range.
    /// </summary>
    private bool Releases(in Attacker unit, in CombatStance stance, Entity target, in AttackRange range)
    {
        var weapon = unit.Mounts.Primary.Weapon;
        if (weapon.FiringArc > 0f && !Targets.InFiringArc(unit.Entity, weapon, target))
        {
            return true;
        }

        var leash = stance.ScanRange(unit.Vision.Value, weapon.Range) * stance.LeashFactor;
        var strays = Roams(stance, unit.Moves)
            ? Strays(unit.Entity, unit.Position, stance, target, leash)
            : range.Gap > range.Band.Reach;
        return strays || range.Gap < range.Band.Floor;
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

    /// <summary>Held and immobile units never move off, so only their reach limits them.</summary>
    private static bool Roams(in CombatStance stance, in UnitMove moves) =>
        stance.Value != Stance.HoldPosition && moves.IsMobile;

    private bool Strays(Entity entity, float3 position, in CombatStance stance, Entity target, float leash)
    {
        // Defensive units measure from their post, so a fleeing target can't drag them away.
        var origin = stance.Value == Stance.Defensive ? stance.ReturnPoint : position;
        return Targets.Gap(entity, origin, target) > leash;
    }

    /// <summary>Widens <paramref name="band"/> to every extra weapon that joins the attack on <paramref name="target"/>.</summary>
    private void ExtendReach(in Attacker unit, Entity target, ref RangeBand band)
    {
        for (var i = 0; i < unit.Mounts.Length; i++)
        {
            var mount = unit.Mounts[i];
            if (i != WeaponMounts.PrimaryIndex && JoinsAttack(mount, target))
            {
                band.Reach = math.max(band.Reach, Targets.Reach(unit.Entity, unit.Position, mount.Weapon, target));
                band.Floor = math.min(band.Floor, mount.Weapon.MinRange);
            }
        }
    }

    /// <summary>A ready extra weapon that shares the unit's target and can hurt it.</summary>
    private bool JoinsAttack(in WeaponMount mount, Entity target)
    {
        if (mount.Independent || !mount.Ready)
        {
            return false;
        }

        return Targets.CanHurt(target, mount.Weapon);
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
}
