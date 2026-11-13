using HyperRTS.Simulation.Navigation.Drive;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Movement
{
    /// <summary>
    /// How a ground vehicle speeds up, brakes, turns and reverses, driven each frame by a <see cref="Driver"/>. Units
    /// without it move by <see cref="Instant"/>.
    /// </summary>
    public struct Locomotion : IComponentData
    {
        public float Acceleration;
        public float Braking;

        /// <summary>Radians per second the hull yaws at most: the turn rate of tracks, the grip limit of wheels.</summary>
        public float TurnRate;

        /// <summary>Radians per second the steered wheels turn.</summary>
        public float SteerRate;

        public float ReverseSpeed;

        // Long enough to back about a hull length off the walls before turning.
        private const float TurnOutSeconds = 3f;
        private const float Unlimited = 1e4f;
        private const float Instantly = 1e6f;

        /// <summary>Handling of units without their own: full speed and any facing at once, never reversing.</summary>
        public static Locomotion Instant => new() { Acceleration = Instantly, Braking = Instantly, TurnRate = Instantly };

        /// <summary>Whether it handles as <see cref="Instant"/>: any facing at once.</summary>
        public readonly bool IsInstant => TurnRate >= Instantly;

        /// <summary>Whether it can back out of a wedge: any vehicle that reverses.</summary>
        public readonly bool CanTurnOut => !IsInstant && ReverseSpeed > 0f;

        /// <summary>Drives the other way a while, wheels or tracks, to turn out of a wedge the walls hold it in.</summary>
        public readonly void TurnOut(ref LocomotionState state) => state.OtherWay = TurnOutSeconds;

        /// <summary>The speed after one frame of speeding up or braking toward <paramref name="target"/>.</summary>
        internal readonly float Accelerate(float speed, float target, float deltaTime)
        {
            var speeding = speed * target >= 0f && math.abs(target) > math.abs(speed);
            var rate = (speeding ? Acceleration : Braking) * deltaTime;
            return speed + math.clamp(target - speed, -rate, rate);
        }

        /// <summary>Fastest it can follow the arc to a point that far off its heading without exceeding its turn rate.</summary>
        internal readonly float TurnSpeed(float turnRadius, float distance, float error)
        {
            var turn = math.abs(error);
            if (turn < 1e-3f)
            {
                return Unlimited;
            }

            var arc = turn < math.PI * 0.5f ? distance / (2f * math.sin(turn)) : 0f;
            return TurnRate * math.max(turnRadius, arc);
        }

        /// <summary>Fastest round a corner cut from the look-ahead point, an arc of this radius.</summary>
        internal readonly float CornerSpeed(float turnRadius, float cornerTurn, float lookAhead)
        {
            var half = cornerTurn * 0.5f;
            return half < 1e-3f ? Unlimited : TurnRate * math.max(turnRadius, lookAhead / math.tan(half));
        }

        /// <summary>Radians the hull can turn within <paramref name="seconds"/> at <paramref name="speed"/>.</summary>
        internal readonly float TurnWithin(in Chassis chassis, float speed, float seconds)
        {
            if (!chassis.IsWheeled)
            {
                return math.min(math.PI, TurnRate * seconds);
            }

            // Wheels first steer over to the tightest curvature at the steer rate, then hold it.
            var curvature = Grip(chassis, speed);
            var steering = math.atan(chassis.Wheelbase * curvature) / SteerRate;
            var held = seconds > steering ? seconds - steering * 0.5f : seconds * seconds / (2f * steering);
            return math.min(math.PI, math.abs(speed) * curvature * held);
        }

        /// <summary>The curvature of the steering after one frame of turning the wheels toward <paramref name="wanted"/>.</summary>
        internal readonly float SteerToward(in Chassis chassis, float steer, float speed, float wanted, float deltaTime)
        {
            var limit = Grip(chassis, speed);
            var angle = math.atan(chassis.Wheelbase * steer);
            var target = math.atan(chassis.Wheelbase * math.clamp(wanted, -limit, limit));
            var turn = SteerRate * deltaTime;
            angle += math.clamp(target - angle, -turn, turn);
            return math.clamp(math.tan(angle) / chassis.Wheelbase, -limit, limit);
        }

        // The turn radius, and at speed the yaw rate the tyres hold.
        private readonly float Grip(in Chassis chassis, float speed) =>
            math.min(chassis.MaxCurvature, TurnRate / math.max(math.abs(speed), 1e-3f));
    }
}
