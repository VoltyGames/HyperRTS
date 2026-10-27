using Unity.Mathematics;

namespace HyperRTS.Simulation.Units
{
    public partial struct Driver
    {
        /// <summary>Radians the hull can turn within <paramref name="seconds"/> at <paramref name="speed"/>.</summary>
        public readonly float TurnWithin(float speed, float seconds)
        {
            if (!Chassis.IsWheeled)
            {
                return math.min(math.PI, Handling.TurnRate * seconds);
            }

            // Wheels first steer over to the tightest curvature at the steer rate, then hold it.
            var curvature = Grip(speed);
            var steering = math.atan(Chassis.Wheelbase * curvature) / Handling.SteerRate;
            var held = seconds > steering ? seconds - steering * 0.5f : seconds * seconds / (2f * steering);
            return math.min(math.PI, math.abs(speed) * curvature * held);
        }

        // The rear axle aims at the point the look-ahead reaches along the path; tracks head along the leg.
        private readonly float2 ToTarget(in PathLeg leg, float lookAhead)
        {
            if (!Chassis.IsWheeled)
            {
                return leg.Direction;
            }

            return leg.PointAhead(Position, lookAhead) - Chassis.Pose(Position, Facing).Position;
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
        private float2 Roll(float wanted)
        {
            if (Chassis.IsWheeled)
            {
                State.Steer = SteerToward(wanted);
            }

            var pose = Chassis.Pose(Position, Facing).Advance(State.Steer, State.Speed * DeltaTime);
            Facing = math.normalize(pose.Facing);
            return Chassis.Center(pose) - Position;
        }

        private readonly float SteerToward(float wanted)
        {
            var limit = Grip(State.Speed);
            var angle = math.atan(Chassis.Wheelbase * State.Steer);
            var target = math.atan(Chassis.Wheelbase * math.clamp(wanted, -limit, limit));
            var turn = Handling.SteerRate * DeltaTime;
            angle += math.clamp(target - angle, -turn, turn);
            return math.clamp(math.tan(angle) / Chassis.Wheelbase, -limit, limit);
        }

        // The turn radius, and at speed the yaw rate the tyres hold.
        private readonly float Grip(float speed) =>
            math.min(Chassis.MaxCurvature, Handling.TurnRate / math.max(math.abs(speed), 1e-3f));
    }
}
