using HyperRTS.Simulation.Navigation;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Units
{
    public partial struct Locomotion
    {
        /// <summary>Radians the hull can turn within <paramref name="seconds"/> at <paramref name="speed"/>.</summary>
        public readonly float TurnWithin(in Chassis chassis, float speed, float seconds)
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

        // The rear axle aims at the point the look-ahead reaches along the path; tracks head along the leg.
        private static float2 ToTarget(in Chassis chassis, float2 position, float2 facing, in PathLeg leg,
            float lookAhead)
        {
            if (!chassis.IsWheeled)
            {
                return leg.Direction;
            }

            return leg.PointAhead(position, lookAhead) - chassis.Pose(position, facing).Position;
        }

        /// <summary>
        /// Pure pursuit: the curvature of the arc from the rear axle to a target <paramref name="error"/> off the
        /// heading. A target behind takes full lock.
        /// </summary>
        private static float Pursuit(float error, float distance)
        {
            if (math.abs(error) > math.PI * 0.5f)
            {
                return math.sign(error) * float.PositiveInfinity;
            }

            return 2f * math.sin(error) / math.max(distance, 1e-3f);
        }

        /// <summary>Steers toward <paramref name="wanted"/> and rolls the rear axle along the arc.</summary>
        private readonly float2 Roll(ref LocomotionState state, in Chassis chassis, float2 position, ref float2 facing,
            float wanted, float deltaTime)
        {
            if (chassis.IsWheeled)
            {
                state.Steer = SteerToward(chassis, state.Steer, wanted, state.Speed, deltaTime);
            }

            var pose = chassis.Advance(chassis.Pose(position, facing), state.Steer, state.Speed * deltaTime);
            facing = math.normalize(pose.Facing);
            return chassis.Center(pose) - position;
        }

        private readonly float SteerToward(in Chassis chassis, float steer, float wanted, float speed,
            float deltaTime)
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
