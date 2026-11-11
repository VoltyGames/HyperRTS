using HyperRTS.Simulation.Combat.Targeting;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Players;
using HyperRTS.Simulation.Movement.Crushing;
using HyperRTS.Simulation.Navigation.Spatial;
using HyperRTS.Simulation.Orders;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Movement
{
    /// <summary>
    /// Who moves when two units meet: who gives way, each mover's share of the avoiding, who is ahead, who is crushed,
    /// and the lane ahead of a vehicle that allied foot units clear. Read-only, so parallel jobs can share it.
    /// </summary>
    public struct RightOfWay
    {
        /// <summary>Metres per second a vehicle keeps to while allied foot units stand in its lane.</summary>
        public const float WalkingPace = 1.5f;

        /// <summary>Metres past its own reach a foot unit looks for vehicle lanes, more than any vehicle brakes in.</summary>
        public const float LaneSight = 10f;

        // Past the braking distance, so a soldier has time at a walk to clear the lane before contact.
        private const float LaneLead = 4f;

        // A foot unit steps this far past the lane's edge, so it doesn't stand on it.
        private const float LaneMargin = 0.1f;

        // Heavier by half again has the right of way.
        private const float Outweighing = 1.5f;

        // Goals within 60 degrees of each other share a heading, so a crossing pair still shares the avoiding.
        private const float SharedHeading = 0.5f;

        // Slower than this a vehicle's lane runs along its facing, as its velocity has no direction.
        private const float Rolling = 0.1f;

        [ReadOnly] private ComponentLookup<ActiveOrder> _orders;
        [ReadOnly] private ComponentLookup<AttackTarget> _attacks;
        [ReadOnly] private ComponentLookup<CombatStance> _stances;
        [ReadOnly] private ComponentLookup<Locomotion> _handling;
        [ReadOnly] private ComponentLookup<Crusher> _crushers;
        [ReadOnly] private ComponentLookup<Crushable> _crushables;
        private OwnerRelations _relations;

        public RightOfWay(ref SystemState state)
        {
            _orders = state.GetComponentLookup<ActiveOrder>(true);
            _attacks = state.GetComponentLookup<AttackTarget>(true);
            _stances = state.GetComponentLookup<CombatStance>(true);
            _handling = state.GetComponentLookup<Locomotion>(true);
            _crushers = state.GetComponentLookup<Crusher>(true);
            _crushables = state.GetComponentLookup<Crushable>(true);
            _relations = default;
        }

        public void Update(ref SystemState state, in OwnerRelations relations)
        {
            _orders.Update(ref state);
            _attacks.Update(ref state);
            _stances.Update(ref state);
            _handling.Update(ref state);
            _crushers.Update(ref state);
            _crushables.Update(ref state);
            _relations = relations;
        }

        /// <summary>
        /// Whether <paramref name="crusher"/> drives over <paramref name="victim"/> instead of round it: an enemy foot
        /// unit its crusher is big enough for.
        /// </summary>
        public readonly bool Crushes(in SpatialEntry crusher, in SpatialEntry victim)
        {
            if (!crusher.IsVehicle || victim.IsVehicle || _relations.IsAllied(crusher.Owner, victim.Owner))
            {
                return false;
            }

            if (!_crushers.TryGetComponent(crusher.Entity, out var size) ||
                !_crushables.TryGetComponent(victim.Entity, out var rating))
            {
                return false;
            }

            return rating.Crush > 0 && rating.Crush <= size.Size;
        }

        /// <summary>
        /// Whether the pair ignore each other: a crusher takes no notice of a unit it drives over, and that unit none of a
        /// crusher at crushing pace, so neither steers, stops or pushes the other.
        /// </summary>
        public readonly bool IsCrushing(in SpatialEntry unit, in SpatialEntry other)
        {
            if (Crushes(unit, other))
            {
                return true;
            }

            var pace = Crusher.Pace;
            return math.lengthsq(other.Velocity) > pace * pace && Crushes(other, unit);
        }

        /// <summary>
        /// Whether <paramref name="unit"/> gives way to <paramref name="mover"/> of its team. A foot unit always does
        /// for a vehicle, and a vehicle never does for a foot unit; otherwise only a parked idle unit does, as an
        /// order, a target or Hold Position holds its ground.
        /// </summary>
        public readonly bool Yields(in SpatialEntry unit, in SpatialEntry mover)
        {
            if (!_relations.IsAllied(unit.Owner, mover.Owner))
            {
                return false;
            }

            if (unit.IsVehicle != mover.IsVehicle)
            {
                return mover.IsVehicle;
            }

            return !unit.IsMoving && IsIdle(unit.Entity);
        }

        /// <summary>
        /// The mover's part of steering round <paramref name="other"/>: none when the other gives way, so it isn't
        /// steered round; all against a unit holding its ground, in same-way traffic or against a mover that
        /// outweighs it; else half. Takes what the caller already worked out for the pair.
        /// </summary>
        public readonly float Share(in SpatialEntry mover, in SpatialEntry other, bool otherYields, int precedence)
        {
            if (otherYields)
            {
                return 0f;
            }

            if (!other.IsMoving || precedence != 0)
            {
                return 1f;
            }

            return Outweighs(other, mover) ? 1f : 0.5f;
        }

        /// <summary>
        /// The mover's part of the overlap push against <paramref name="other"/>: all of it between a foot unit and a
        /// vehicle, which the push never moves; half against a unit that moves or gives way too; else all.
        /// </summary>
        public readonly float PushShare(in SpatialEntry mover, in SpatialEntry other, bool otherYields)
        {
            if (mover.IsVehicle != other.IsVehicle)
            {
                return mover.IsVehicle ? 0f : 1f;
            }

            return other.IsMoving || otherYields ? 0.5f : 1f;
        }

        /// <summary>
        /// 1 when the mover is ahead of <paramref name="other"/> going the same way, -1 behind, 0 otherwise. Two
        /// movers going one way at a gap too narrow for both would each wait on the other, so the one behind gives way.
        /// </summary>
        public readonly int Precedence(in SpatialEntry mover, in SpatialEntry other)
        {
            if (!mover.IsMoving || !other.IsMoving || math.dot(mover.Heading, other.Heading) < SharedHeading)
            {
                return 0;
            }

            var ahead = math.dot(mover.Position.xz - other.Position.xz, mover.Heading + other.Heading);
            if (ahead != 0f)
            {
                return ahead > 0f ? 1 : -1;
            }

            return mover.Entity.Index < other.Entity.Index ? 1 : -1;
        }

        /// <summary>
        /// Whether <paramref name="unit"/> stands in the lane ahead of <paramref name="vehicle"/>: its hull's width
        /// with spacing, from its centre to its braking distance past the nose and a lead.
        /// </summary>
        public readonly bool IsInLane(in SpatialEntry vehicle, in SpatialEntry unit)
        {
            var travel = Travel(vehicle);
            return IsInLane(vehicle, unit, travel, unit.Position.xz - vehicle.Position.xz);
        }

        /// <summary>The move that takes <paramref name="unit"/> out of the lane ahead of <paramref name="vehicle"/>, to the side it leans to.</summary>
        public readonly float2 StepAside(in SpatialEntry vehicle, in SpatialEntry unit)
        {
            var travel = Travel(vehicle);
            var offset = unit.Position.xz - vehicle.Position.xz;
            if (!IsInLane(vehicle, unit, travel, offset))
            {
                return float2.zero;
            }

            var outside = LaneHalfWidth(vehicle, unit) + LaneMargin - math.abs(Planar.Cross(travel, offset));
            return Side(travel, offset) * outside;
        }

        private readonly bool IsInLane(in SpatialEntry vehicle, in SpatialEntry unit, float2 travel, float2 offset)
        {
            var along = math.dot(offset, travel);
            if (along <= 0f)
            {
                return false;
            }

            var length = vehicle.HullReach + LaneAhead(vehicle) + unit.Radius;
            if (along > length)
            {
                return false;
            }

            return math.abs(Planar.Cross(travel, offset)) < LaneHalfWidth(vehicle, unit);
        }

        /// <summary>
        /// The side of a line along <paramref name="travel"/> that <paramref name="offset"/> from it lies on; left
        /// when on the line, as a mover keeps right.
        /// </summary>
        public static float2 Side(float2 travel, float2 offset)
        {
            var left = Planar.Left(travel);
            return math.dot(offset, left) < 0f ? -left : left;
        }

        private readonly bool IsIdle(Entity unit)
        {
            if (_orders.HasEnabled(unit) || _attacks.HasEnabled(unit))
            {
                return false;
            }

            return !_stances.TryGetComponent(unit, out var stance) || stance.Value != Stance.HoldPosition;
        }

        private static bool Outweighs(in SpatialEntry heavy, in SpatialEntry light) =>
            heavy.Mass > light.Mass && heavy.Mass >= light.Mass * Outweighing;

        private static float2 Travel(in SpatialEntry vehicle)
        {
            var speedSq = math.lengthsq(vehicle.Velocity);
            return speedSq > Rolling * Rolling ? vehicle.Velocity * math.rsqrt(speedSq) : vehicle.Facing;
        }

        private readonly float LaneAhead(in SpatialEntry vehicle) =>
            math.lengthsq(vehicle.Velocity) / (2f * _handling[vehicle.Entity].Braking) + LaneLead;

        private static float LaneHalfWidth(in SpatialEntry vehicle, in SpatialEntry unit) =>
            vehicle.Radius + unit.Radius + math.max(vehicle.Spacing, unit.Spacing);
    }
}
