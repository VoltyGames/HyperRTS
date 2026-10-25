using HyperRTS.Simulation.Navigation;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Units
{
    /// <summary>
    /// How a ground vehicle handles: it speeds up and brakes at set rates, turns at a limited rate and radius,
    /// pivots if tracked and backs up to goals close behind. Units without it move by <see cref="Instant"/>.
    /// </summary>
    public struct Locomotion : IComponentData
    {
        public float Acceleration;
        public float Braking;

        /// <summary>Radians per second.</summary>
        public float TurnRate;

        /// <summary>Tightest circle it can drive; 0 turns at any speed.</summary>
        public float TurnRadius;

        /// <summary>Stops and turns in place when far off its heading, as tracked vehicles do.</summary>
        public bool Pivots;

        public float ReverseSpeed;

        /// <summary>Longest path it backs along, only when turning round would clearly take longer.</summary>
        public float ReverseDistance;

        private const float PivotAngle = math.PI / 3f;
        private const float Crawl = 0.5f;
        private const float LookAheadTime = 0.5f;
        private const float Unlimited = 1e4f;
        private const float ThreePointLegSeconds = 1.5f;
        private const float ReverseMargin = 1.3f;
        private const float Instantly = 1e6f;

        /// <summary>Handling of units without their own: full speed and any facing at once, never reversing.</summary>
        public static Locomotion Instant => new()
        {
            Acceleration = Instantly,
            Braking = Instantly,
            TurnRate = Instantly,
        };

        /// <summary>How far ahead it may cut to a corner in sight: further when fast or wide-turning.</summary>
        public readonly float LookAhead(float speed, float radius) =>
            radius * 2f + math.abs(speed) * LookAheadTime + TurnRadius * 0.5f;

        /// <summary>Accelerates and turns toward the leg, returning this frame's step along the new facing.</summary>
        public readonly float2 Drive(ref LocomotionState state, float2 position, ref float2 facing, in PathLeg leg,
            float topSpeed, float radius, float deltaTime) =>
            Follow(ref state, position, ref facing, leg, topSpeed, topSpeed, radius, deltaTime);

        /// <summary>
        /// Drives toward the avoidance <paramref name="velocity"/> instead of the leg, no faster than it, turning only
        /// as the hull can. A vehicle told to go back brakes on the leg instead of turning round.
        /// </summary>
        public readonly float2 Steer(ref LocomotionState state, float2 position, ref float2 facing, in PathLeg leg,
            float2 velocity, float topSpeed, float radius, float deltaTime)
        {
            var travel = state.Reverse ? -facing : facing;
            if (TurnRate < Instantly && math.dot(velocity, travel) < 0f)
            {
                return Follow(ref state, position, ref facing, leg, topSpeed, 0f, radius, deltaTime);
            }

            var steered = leg.Toward(position, velocity);
            return Follow(ref state, position, ref facing, steered, topSpeed, math.length(velocity), radius, deltaTime);
        }

        private readonly float2 Follow(ref LocomotionState state, float2 position, ref float2 facing, in PathLeg leg,
            float topSpeed, float cap, float radius, float deltaTime)
        {
            state.Reverse = PrefersReverse(state.Reverse, facing, leg, topSpeed);
            state.InThreePointTurn = IsInThreePointTurn(state.InThreePointTurn, position, facing, leg.Corner);
            var desired = state.Reverse ? -leg.Direction : leg.Direction;
            var error = SignedAngle(facing, desired);
            var gear = state.Reverse == IsReverseLeg(ref state, deltaTime) ? 1f : -1f;
            var top = math.min(gear > 0f ? topSpeed : ReverseSpeed, cap);
            // Never past the goal in one frame, which only bites for near-instant braking.
            var limit = math.min(TargetSpeed(error, leg, top, radius, LookAhead(state.Speed, radius)),
                leg.Remaining / deltaTime);
            var target = gear * limit;
            state.Speed = Accelerate(state.Speed, target, deltaTime);
            facing = Turn(facing, error, TurnLimit(state.Speed) * deltaTime);
            return facing * (state.Speed * deltaTime);
        }

        /// <summary>Turns the standing hull toward <paramref name="direction"/> at its turn rate, as when aiming.</summary>
        public readonly float2 TurnToward(float2 facing, float2 direction, float deltaTime) =>
            Turn(facing, SignedAngle(facing, direction), TurnRate * deltaTime);

        /// <summary>
        /// A parked unit with no other turn turns its hull to a hull-fixed weapon's target at its turn rate. The aim
        /// lasts one frame, so it is cleared here.
        /// </summary>
        public readonly float2 Aim(float2 heading, float2 facing, bool parked, in FacingGoal aim,
            EnabledRefRW<FacingGoal> aiming, float deltaTime)
        {
            var turns = parked && aiming.ValueRO && math.all(heading == 0f);
            aiming.ValueRW = false;
            return turns ? TurnToward(facing, aim.Direction, deltaTime) : heading;
        }

        /// <summary>Brakes to a stop along its facing once it has nowhere to go.</summary>
        public readonly float2 BrakeToStop(ref LocomotionState state, float2 facing, float deltaTime)
        {
            state.Speed = Accelerate(state.Speed, 0f, deltaTime);
            state.Reverse = false;
            state.InThreePointTurn = false;
            state.ThreePointTurnBlocked = 0f;
            return facing * (state.Speed * deltaTime);
        }

        public readonly bool HasArrived(in PathLeg leg, float2 position, float2 facing, float radius)
        {
            if (leg.HasArrived(radius))
            {
                return true;
            }

            // A goal under the hull but inside the turning circle would take a three-point turn to reach.
            var underHull = leg.IsFinal && leg.Distance <= radius;
            return underHull && IsInsideTurn(position, facing, leg.Corner, 1f);
        }

        /// <summary>Whether no arc it can drive reaches the target, so it must make a three-point turn first.</summary>
        public readonly bool IsInsideTurn(float2 position, float2 facing, float2 target, float slack)
        {
            if (Pivots || TurnRadius <= 0f)
            {
                return false;
            }

            var side = Cross(facing, target - position) >= 0f ? 1f : -1f;
            var center = position + new float2(-facing.y, facing.x) * (side * TurnRadius);
            return math.distance(target, center) < TurnRadius * slack;
        }

        private readonly bool PrefersReverse(bool reversing, float2 facing, in PathLeg leg, float topSpeed)
        {
            if (leg.Remaining > ReverseDistance)
            {
                return false;
            }

            var along = math.dot(facing, leg.Direction);
            if (along >= (reversing ? 0.3f : -0.2f))
            {
                return false;
            }

            var backing = leg.Remaining / ReverseSpeed;
            var turningRound = TurnSeconds(math.acos(math.max(-1f, along)), topSpeed) + leg.Remaining / topSpeed;
            return turningRound > backing * ReverseMargin;
        }

        // Tracks turn at their rate; wheels also need the arc of their turning circle rolled.
        private readonly float TurnSeconds(float angle, float topSpeed)
        {
            var rolling = Pivots ? 0f : TurnRadius / topSpeed;
            return angle * math.max(1f / TurnRate, rolling);
        }

        // Keeps reversing a little past the circle's edge, so it doesn't flip gear every frame.
        private readonly bool IsInThreePointTurn(bool turning, float2 position, float2 facing, float2 corner) =>
            IsInsideTurn(position, facing, corner, turning ? 1.2f : 1f);

        // A reversing leg that runs into something drives the other leg of the turn for a while.
        private static bool IsReverseLeg(ref LocomotionState state, float deltaTime)
        {
            var hit = state.InThreePointTurn && state.Blocked && state.ThreePointTurnBlocked <= 0f;
            state.ThreePointTurnBlocked =
                hit ? ThreePointLegSeconds : math.max(0f, state.ThreePointTurnBlocked - deltaTime);
            return state.InThreePointTurn && state.ThreePointTurnBlocked <= 0f;
        }

        private readonly float TargetSpeed(float error, in PathLeg leg, float top, float radius, float lookAhead)
        {
            if (Pivots && math.abs(error) > PivotAngle)
            {
                return 0f;
            }

            var tolerance = NavTolerances.Arrive(radius);
            var stop = math.max(Crawl, math.sqrt(2f * Braking * math.max(0f, leg.Remaining - tolerance)));
            var beforeCorner = math.max(0f, leg.Distance - lookAhead);
            var corner = math.sqrt(math.square(CornerSpeed(leg.CornerTurn, lookAhead)) + 2f * Braking * beforeCorner);
            return math.min(math.min(top, TurnSpeed(leg.Distance, error)), math.min(stop, corner));
        }

        // Fastest it can follow the arc to a point that far off its heading without exceeding its turn rate.
        private readonly float TurnSpeed(float distance, float error)
        {
            var turn = math.abs(error);
            if (turn < 1e-3f)
            {
                return Unlimited;
            }

            var arc = turn < math.PI * 0.5f ? distance / (2f * math.sin(turn)) : 0f;
            return TurnRate * math.max(TurnRadius, arc);
        }

        // Cutting the corner from the look-ahead point is an arc of this radius.
        private readonly float CornerSpeed(float cornerTurn, float lookAhead)
        {
            var half = cornerTurn * 0.5f;
            return half < 1e-3f ? Unlimited : TurnRate * math.max(TurnRadius, lookAhead / math.tan(half));
        }

        private readonly float Accelerate(float speed, float target, float deltaTime)
        {
            var speeding = speed * target >= 0f && math.abs(target) > math.abs(speed);
            var rate = (speeding ? Acceleration : Braking) * deltaTime;
            return speed + math.clamp(target - speed, -rate, rate);
        }

        // Wheels turn the hull only as it rolls; tracks turn it standing.
        private readonly float TurnLimit(float speed) =>
            Pivots || TurnRadius <= 0f ? TurnRate : math.min(TurnRate, math.abs(speed) / TurnRadius);

        private static float2 Turn(float2 facing, float error, float limit)
        {
            math.sincos(math.clamp(error, -limit, limit), out var sin, out var cos);
            return math.normalize(new float2(facing.x * cos - facing.y * sin, facing.x * sin + facing.y * cos));
        }

        private static float SignedAngle(float2 from, float2 to) => math.atan2(Cross(from, to), math.dot(from, to));

        private static float Cross(float2 a, float2 b) => a.x * b.y - a.y * b.x;
    }
}
