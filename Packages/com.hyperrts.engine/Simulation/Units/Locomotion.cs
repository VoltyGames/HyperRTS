using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Units
{
    /// <summary>
    /// How a ground vehicle handles: it speeds up and brakes at set rates, steers or turns at a limited rate and backs
    /// up to goals close behind. A <see cref="Driver"/> drives with it each frame. Units without it move by
    /// <see cref="Instant"/>.
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

        /// <summary>Top speed forwards on a planned manoeuvre; backwards it is <see cref="ReverseSpeed"/>.</summary>
        public const float TightSpeed = 3f;

        private const float TightLookAheadMin = 2f;
        private const float TightLookAheadMax = 6f;

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

        /// <summary>How far along a planned manoeuvre its pursuit aims: 2 m standing, up to 6 m at speed.</summary>
        public static float TightLookAhead(float speed) =>
            math.min(TightLookAheadMax, TightLookAheadMin + math.abs(speed) * (TightLookAheadMax - TightLookAheadMin) /
                TightSpeed);

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
    }
}
