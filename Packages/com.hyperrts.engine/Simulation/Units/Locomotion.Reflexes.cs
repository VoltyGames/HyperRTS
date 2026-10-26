using HyperRTS.Simulation.Navigation;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Units
{
    public partial struct Locomotion
    {
        private const float OtherWaySeconds = 1.5f;

        // Long enough to back about a hull length off the walls before turning.
        private const float TurnOutSeconds = 3f;
        private const float ReverseMargin = 1.3f;

        private readonly bool PrefersReverse(in Chassis chassis, bool reversing, float2 facing, in PathLeg leg,
            float topSpeed)
        {
            if (leg.Remaining > chassis.ReverseRun)
            {
                return false;
            }

            var along = math.dot(facing, leg.Direction);
            if (along >= (reversing ? 0.3f : -0.2f))
            {
                return false;
            }

            var backing = leg.Remaining / ReverseSpeed;
            var turnSeconds = TurnSeconds(chassis, math.acos(math.max(-1f, along)), topSpeed);
            return turnSeconds + leg.Remaining / topSpeed > backing * ReverseMargin;
        }

        // Tracks turn at their rate; wheels also need the arc of their turning circle rolled.
        private readonly float TurnSeconds(in Chassis chassis, float angle, float topSpeed)
        {
            var rolling = chassis.Pivots ? 0f : chassis.TurnRadius / topSpeed;
            return angle * math.max(1f / TurnRate, rolling);
        }

        /// <summary>Wheels can't turn standing, so a wheeled hull wedged against a wall backs off it a while.</summary>
        public static void BackOff(in Chassis chassis, ref LocomotionState state)
        {
            if (chassis.IsWheeled && state.OtherWay <= 0f)
            {
                state.OtherWay = OtherWaySeconds;
            }
        }

        /// <summary>Whether it can back out of a wedge: any vehicle that reverses.</summary>
        public readonly bool CanTurnOut => !IsInstant && ReverseSpeed > 0f;

        /// <summary>Drives the other way a while, wheels or tracks, to turn out of a wedge the walls hold it in.</summary>
        public readonly void TurnOut(ref LocomotionState state) => state.OtherWay = TurnOutSeconds;

        // Wheels can't turn standing, so a leg that runs head-on into a wall drives the other way for a while,
        // backing off to turn or taking a three-point turn's forward leg. A manoeuvre timer, not a stuck test.
        private static bool IsOtherWay(in Chassis chassis, ref LocomotionState state, float deltaTime)
        {
            var hit = chassis.IsWheeled && state.Blocked && state.OtherWay <= 0f;
            state.OtherWay = hit ? OtherWaySeconds : math.max(0f, state.OtherWay - deltaTime);
            return state.InThreePointTurn != state.OtherWay > 0f;
        }
    }
}
