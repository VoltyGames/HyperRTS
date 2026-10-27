using Unity.Mathematics;

namespace HyperRTS.Simulation.Units
{
    public partial struct Driver
    {
        private const float OtherWaySeconds = 1.5f;
        private const float ReverseMargin = 1.3f;

        private readonly bool PrefersReverse(in PathLeg leg)
        {
            if (leg.Remaining > Chassis.ReverseRun)
            {
                return false;
            }

            var along = math.dot(Facing, leg.Direction);
            if (along >= (State.Reverse ? 0.3f : -0.2f))
            {
                return false;
            }

            var backing = leg.Remaining / Handling.ReverseSpeed;
            // Tracks turn at their rate; wheels also need the arc of their turning circle rolled.
            var rolling = Chassis.Pivots ? 0f : Chassis.TurnRadius / TopSpeed;
            var turning = math.acos(math.max(-1f, along)) * math.max(1f / Handling.TurnRate, rolling);
            return turning + leg.Remaining / TopSpeed > backing * ReverseMargin;
        }

        /// <summary>Wheels can't turn standing, so a wheeled hull wedged against a wall backs off it a while.</summary>
        public void BackOff()
        {
            if (Chassis.IsWheeled && State.OtherWay <= 0f)
            {
                State.OtherWay = OtherWaySeconds;
            }
        }

        // A leg that runs head-on into a wall drives the other way a while, backing off to turn or taking a
        // three-point turn's forward leg. A manoeuvre timer, not a stuck test.
        private bool IsOtherWay()
        {
            var hit = Chassis.IsWheeled && State.Blocked && State.OtherWay <= 0f;
            State.OtherWay = hit ? OtherWaySeconds : math.max(0f, State.OtherWay - DeltaTime);
            return State.InThreePointTurn != State.OtherWay > 0f;
        }
    }
}
