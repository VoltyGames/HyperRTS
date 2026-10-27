using Unity.Mathematics;

namespace HyperRTS.Simulation.Units
{
    internal partial struct Steering
    {
        /// <summary>
        /// Drives the leg, steering when the planned velocity would hit something. A planned manoeuvre isn't steered,
        /// which would break its legs; it waits instead.
        /// </summary>
        public float2 Drive(ref Driver driver, in PathLeg leg) =>
            leg.Tight ? DriveTight(ref driver, leg) : DriveSampled(ref driver, leg);

        // Stands while driving on would run deeper into a neighbour than standing would. One held too long replans,
        // so two waiting on each other can't deadlock.
        private float2 DriveTight(ref Driver driver, in PathLeg leg)
        {
            var start = driver;
            var step = driver.Drive(leg);
            if (RunsInto(step / driver.DeltaTime))
            {
                driver = start;
                step = driver.Steer(leg, float2.zero);
            }

            driver.State.LastChoice = step / driver.DeltaTime;
            return step;
        }

        private readonly bool RunsInto(float2 velocity) =>
            NeighbourOverlap(velocity) > NeighbourOverlap(float2.zero) + Unchanged;

        private readonly float NeighbourOverlap(float2 candidate)
        {
            var overlap = 0f;
            foreach (var neighbour in _neighbours)
            {
                overlap += neighbour.Overlap(_position, _axis, _velocity, candidate, 0f, Horizon);
            }

            return overlap;
        }
    }
}
