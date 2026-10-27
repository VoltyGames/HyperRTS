using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Navigation;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Units
{
    public partial struct Driver
    {
        // Within this of a gear change the aim sits on the hull, so the wheels hold their angle.
        private const float SteerHold = 1f;

        // Until the wheels turn to within this of the planned angle it only creeps, so it keeps to the plan.
        private static readonly float SteerSettled = math.radians(3f);

        // Follows the planned legs in their gear with none of the reflexes, braking to each gear change as to the
        // goal and changing gear standing.
        private float2 FollowTight(in PathLeg leg, float cap)
        {
            State.Reverse = leg.Reverse;
            State.InThreePointTurn = false;
            State.OtherWay = 0f;
            var gear = leg.Reverse ? -1f : 1f;
            var aim = Chassis.IsWheeled ? leg.Aim - Chassis.Pose(Position, Facing).Position : leg.Direction;
            var desired = math.normalizesafe(leg.Reverse ? -aim : aim, Facing);
            var error = Planar.SignedAngle(Facing, desired);
            var top = math.min(leg.Reverse ? Handling.ReverseSpeed : Locomotion.TightSpeed, cap);
            var stop = math.max(0f, leg.Stop - NavTolerances.Arrive(Radius));
            var target = math.min(top, math.max(Crawl, math.sqrt(2f * Handling.Braking * stop)));
            var wanted = Chassis.IsWheeled && leg.Stop > SteerHold ? CenterPursuit(aim) : State.Steer;
            var changingGear = State.Speed * gear < -1e-3f;
            if (changingGear || IsPivot(error))
            {
                target = 0f;
            }

            if (IsSteeringOver(wanted))
            {
                target = math.min(target, Crawl);
            }

            State.Speed = Handling.Accelerate(State.Speed, gear * math.min(target, leg.Stop / DeltaTime), DeltaTime);
            return Chassis.IsWheeled ? Roll(wanted) : TurnTracks(error);
        }

        private readonly bool IsSteeringOver(float wanted)
        {
            if (!Chassis.IsWheeled)
            {
                return false;
            }

            var most = Chassis.MaxCurvature;
            var turn = math.atan(Chassis.Wheelbase * math.clamp(wanted, -most, most));
            return math.abs(turn - math.atan(Chassis.Wheelbase * State.Steer)) > SteerSettled;
        }

        /// <summary>
        /// Pursuit that brings the hull centre, not the rear axle, onto the aim, either way: the rear axle's arc whose
        /// circle through the centre also passes through the aim, so planned centre waypoints are followed exactly.
        /// </summary>
        private readonly float CenterPursuit(float2 aim)
        {
            var ahead = math.dot(aim, Facing);
            var side = math.dot(aim, Planar.Left(Facing));
            var spread = ahead * ahead + side * side - Chassis.RearAxle * Chassis.RearAxle;
            return math.abs(spread) > 1e-3f ? 2f * side / spread : math.sign(side) * float.PositiveInfinity;
        }
    }
}
