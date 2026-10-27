using HyperRTS.Simulation.Navigation;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Units
{
    /// <summary>
    /// How a ground vehicle handles: it speeds up and brakes at set rates, steers or turns at a limited rate and backs
    /// up to goals close behind. Its <see cref="Chassis"/> says how the hull turns. Units without it move by
    /// <see cref="Instant"/>.
    /// </summary>
    public partial struct Locomotion : IComponentData
    {
        public float Acceleration;
        public float Braking;

        /// <summary>Radians per second the hull yaws at most: the turn rate of tracks, the grip limit of wheels.</summary>
        public float TurnRate;

        /// <summary>Radians per second the steered wheels turn.</summary>
        public float SteerRate;

        public float ReverseSpeed;

        private const float PivotAngle = math.PI / 3f;
        private const float Crawl = 0.5f;
        private const float LookAheadTime = 0.5f;
        private const float Unlimited = 1e4f;
        private const float Instantly = 1e6f;

        /// <summary>Handling of units without their own: full speed and any facing at once, never reversing.</summary>
        public static Locomotion Instant => new() { Acceleration = Instantly, Braking = Instantly, TurnRate = Instantly };

        /// <summary>Whether it handles as <see cref="Instant"/>: any facing at once.</summary>
        public readonly bool IsInstant => TurnRate >= Instantly;

        /// <summary>How far ahead it may cut to a corner in sight: further when fast or wide-turning.</summary>
        public readonly float LookAhead(in Chassis chassis, float speed, float radius) =>
            radius * 2f + math.abs(speed) * LookAheadTime + chassis.TurnRadius * 0.5f;

        /// <summary>Whether it stands to turn toward the leg rather than drive on.</summary>
        public readonly bool IsPivoting(in LocomotionState state, in Chassis chassis, float2 facing, in PathLeg leg) =>
            state.OtherWay <= 0f && IsPivot(chassis, SignedAngle(facing, state.Reverse ? -leg.Direction : leg.Direction));

        private static bool IsPivot(in Chassis chassis, float error) => chassis.Pivots && math.abs(error) > PivotAngle;

        /// <summary>Accelerates and turns toward the leg, returning this frame's move of the hull centre.</summary>
        public readonly float2 Drive(ref LocomotionState state, in Chassis chassis, float2 position, ref float2 facing,
            in PathLeg leg, float topSpeed, float radius, float deltaTime) =>
            Follow(ref state, chassis, position, ref facing, leg, topSpeed, topSpeed, radius, deltaTime);

        /// <summary>
        /// Drives toward the steering <paramref name="velocity"/> instead of the leg, no faster than it: the velocity
        /// becomes the curvature it steers for. A vehicle told to go back backs straight up if it can reverse, else
        /// brakes on the leg.
        /// </summary>
        public readonly float2 Steer(ref LocomotionState state, in Chassis chassis, float2 position, ref float2 facing,
            in PathLeg leg, float2 velocity, float topSpeed, float radius, float deltaTime)
        {
            // Told to stand, it keeps turning toward the leg, as a pivot does.
            if (math.lengthsq(velocity) < 1e-6f)
            {
                return Follow(ref state, chassis, position, ref facing, leg, topSpeed, 0f, radius, deltaTime);
            }

            var travel = state.Reverse ? -facing : facing;
            if (TurnRate < Instantly && math.dot(velocity, travel) < 0f)
            {
                return GoBack(ref state, chassis, position, ref facing, leg, math.length(velocity), topSpeed, radius,
                    deltaTime);
            }

            var steered = leg.Toward(position, velocity);
            return Follow(ref state, chassis, position, ref facing, steered, topSpeed, math.length(velocity), radius,
                deltaTime);
        }

        private readonly float2 GoBack(ref LocomotionState state, in Chassis chassis, float2 position,
            ref float2 facing, in PathLeg leg, float speed, float topSpeed, float radius, float deltaTime)
        {
            if (state.Reverse || ReverseSpeed <= 0f)
            {
                return Follow(ref state, chassis, position, ref facing, leg, topSpeed, 0f, radius, deltaTime);
            }

            state.Speed = Accelerate(state.Speed, -math.min(speed, ReverseSpeed), deltaTime);
            return Roll(ref state, chassis, position, ref facing, 0f, deltaTime);
        }

        private readonly float2 Follow(ref LocomotionState state, in Chassis chassis, float2 position,
            ref float2 facing, in PathLeg leg, float topSpeed, float cap, float radius, float deltaTime)
        {
            if (leg.Tight)
            {
                return FollowTight(ref state, chassis, position, ref facing, leg, cap, radius, deltaTime);
            }

            state.Reverse = PrefersReverse(chassis, state.Reverse, facing, leg, topSpeed);
            // Keeps reversing a little past the circle's edge, so it doesn't flip gear every frame.
            state.InThreePointTurn =
                chassis.IsInsideTurn(position, facing, leg.Corner, state.InThreePointTurn ? 1.2f : 1f);
            var lookAhead = LookAhead(chassis, state.Speed, radius);
            var aim = ToTarget(chassis, position, facing, leg, lookAhead);
            var desired = math.normalizesafe(state.Reverse ? -aim : aim, facing);
            var error = SignedAngle(facing, desired);
            var gear = state.Reverse == IsOtherWay(chassis, ref state, deltaTime) ? 1f : -1f;
            var top = math.min(gear > 0f ? topSpeed : ReverseSpeed, cap);
            // Pivoting is what a wedge blocks, so tracks back out of one and turn as they go.
            var backsOut = chassis.Pivots && state.OtherWay > 0f;
            // Never past the goal in one frame, which only bites for near-instant braking.
            var target = math.min(TargetSpeed(chassis, error, leg, top, radius, lookAhead, backsOut),
                leg.Remaining / deltaTime);
            state.Speed = Accelerate(state.Speed, gear * target, deltaTime);
            if (chassis.IsWheeled)
            {
                var wanted = gear * Pursuit(error, math.length(aim));
                return Roll(ref state, chassis, position, ref facing, wanted, deltaTime);
            }

            var limit = TurnRate * deltaTime;
            facing = chassis.Rotate(chassis.Pose(position, facing), math.clamp(error, -limit, limit)).Facing;
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
            state.OtherWay = 0f;
            state.LastChoice = float2.zero;
            return facing * (state.Speed * deltaTime);
        }

        public readonly bool HasArrived(in Chassis chassis, in PathLeg leg, float2 position, float2 facing,
            float radius)
        {
            if (leg.HasArrived(radius))
            {
                return true;
            }

            // A goal under the hull but inside the turning circle would take a three-point turn to reach.
            var underHull = leg.IsFinal && leg.Distance <= radius;
            return underHull && chassis.IsInsideTurn(position, facing, leg.Corner, 1f);
        }

        private readonly float TargetSpeed(in Chassis chassis, float error, in PathLeg leg, float top, float radius,
            float lookAhead, bool backsOut)
        {
            // Backing out of a wedge runs straight off the walls, whatever the path's turns.
            if (backsOut)
            {
                return top;
            }

            if (IsPivot(chassis, error))
            {
                return 0f;
            }

            var tolerance = NavTolerances.Arrive(radius);
            var stop = math.max(Crawl, math.sqrt(2f * Braking * math.max(0f, leg.Remaining - tolerance)));
            var beforeCorner = math.max(0f, leg.Distance - lookAhead);
            var cornerSpeed = CornerSpeed(chassis.TurnRadius, leg.CornerTurn, lookAhead);
            var corner = math.sqrt(math.square(cornerSpeed) + 2f * Braking * beforeCorner);
            var turn = TurnSpeed(chassis.TurnRadius, leg.Distance, error);
            return math.min(math.min(top, turn), math.min(stop, corner));
        }

        // Fastest it can follow the arc to a point that far off its heading without exceeding its turn rate.
        private readonly float TurnSpeed(float turnRadius, float distance, float error)
        {
            var turn = math.abs(error);
            if (turn < 1e-3f)
            {
                return Unlimited;
            }

            var arc = turn < math.PI * 0.5f ? distance / (2f * math.sin(turn)) : 0f;
            return TurnRate * math.max(turnRadius, arc);
        }

        // Cutting the corner from the look-ahead point is an arc of this radius.
        private readonly float CornerSpeed(float turnRadius, float cornerTurn, float lookAhead)
        {
            var half = cornerTurn * 0.5f;
            return half < 1e-3f ? Unlimited : TurnRate * math.max(turnRadius, lookAhead / math.tan(half));
        }

        private readonly float Accelerate(float speed, float target, float deltaTime)
        {
            var speeding = speed * target >= 0f && math.abs(target) > math.abs(speed);
            var rate = (speeding ? Acceleration : Braking) * deltaTime;
            return speed + math.clamp(target - speed, -rate, rate);
        }

        private static float2 Turn(float2 facing, float error, float limit)
        {
            math.sincos(math.clamp(error, -limit, limit), out var sin, out var cos);
            return math.normalize(new float2(facing.x * cos - facing.y * sin, facing.x * sin + facing.y * cos));
        }

        private static float SignedAngle(float2 from, float2 to) => math.atan2(Cross(from, to), math.dot(from, to));

        private static float Cross(float2 a, float2 b) => a.x * b.y - a.y * b.x;
    }
}
