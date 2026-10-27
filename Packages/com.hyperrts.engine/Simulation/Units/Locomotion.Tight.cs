using HyperRTS.Simulation.Navigation;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Units
{
    public partial struct Locomotion
    {
        /// <summary>Top speed forwards on a planned manoeuvre; backwards it is <see cref="ReverseSpeed"/>.</summary>
        public const float TightSpeed = 3f;

        private const float TightLookAheadMin = 2f;
        private const float TightLookAheadMax = 6f;

        // Within this of a gear change the aim sits on the hull, so the wheels hold their angle.
        private const float SteerHold = 1f;

        // Until the wheels turn to within this of the planned angle it only creeps, so it keeps to the plan.
        private static readonly float SteerSettled = math.radians(3f);

        /// <summary>How far along a planned manoeuvre its pursuit aims: 2 m standing, up to 6 m at speed.</summary>
        public static float TightLookAhead(float speed) =>
            math.min(TightLookAheadMax, TightLookAheadMin + math.abs(speed) * (TightLookAheadMax - TightLookAheadMin) /
                TightSpeed);

        // Follows the planned legs in their gear with none of the reflexes, braking to each gear change as to the
        // goal and changing gear standing.
        private readonly float2 FollowTight(ref LocomotionState state, in Chassis chassis, float2 position,
            ref float2 facing, in PathLeg leg, float cap, float radius, float deltaTime)
        {
            state.Reverse = leg.Reverse;
            state.InThreePointTurn = false;
            state.OtherWay = 0f;
            var gear = leg.Reverse ? -1f : 1f;
            var aim = chassis.IsWheeled ? leg.Aim - chassis.Pose(position, facing).Position : leg.Direction;
            var desired = math.normalizesafe(leg.Reverse ? -aim : aim, facing);
            var error = SignedAngle(facing, desired);
            var top = math.min(leg.Reverse ? ReverseSpeed : TightSpeed, cap);
            var stop = math.max(0f, leg.Stop - NavTolerances.Arrive(radius));
            var target = math.min(top, math.max(Crawl, math.sqrt(2f * Braking * stop)));
            var wanted = chassis.IsWheeled && leg.Stop > SteerHold ? CenterPursuit(chassis, facing, aim) : state.Steer;
            var changingGear = state.Speed * gear < -1e-3f;
            if (changingGear || IsPivot(chassis, error))
            {
                target = 0f;
            }

            if (IsSteeringOver(chassis, state, wanted))
            {
                target = math.min(target, Crawl);
            }

            state.Speed = Accelerate(state.Speed, gear * math.min(target, leg.Stop / deltaTime), deltaTime);
            if (chassis.IsWheeled)
            {
                return Roll(ref state, chassis, position, ref facing, wanted, deltaTime);
            }

            var limit = TurnRate * deltaTime;
            facing = chassis.Rotate(chassis.Pose(position, facing), math.clamp(error, -limit, limit)).Facing;
            return facing * (state.Speed * deltaTime);
        }

        private static bool IsSteeringOver(in Chassis chassis, in LocomotionState state, float wanted)
        {
            if (!chassis.IsWheeled)
            {
                return false;
            }

            var most = chassis.MaxCurvature;
            var turn = math.atan(chassis.Wheelbase * math.clamp(wanted, -most, most));
            return math.abs(turn - math.atan(chassis.Wheelbase * state.Steer)) > SteerSettled;
        }

        /// <summary>
        /// Pursuit that brings the hull centre, not the rear axle, onto the aim, either way: the rear axle's arc whose
        /// circle through the centre also passes through the aim, so planned centre waypoints are followed exactly.
        /// </summary>
        private static float CenterPursuit(in Chassis chassis, float2 facing, float2 aim)
        {
            var ahead = math.dot(aim, facing);
            var side = math.dot(aim, new float2(-facing.y, facing.x));
            var spread = ahead * ahead + side * side - chassis.RearAxle * chassis.RearAxle;
            return math.abs(spread) > 1e-3f ? 2f * side / spread : math.sign(side) * float.PositiveInfinity;
        }
    }
}
