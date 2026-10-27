using Unity.Mathematics;

namespace HyperRTS.Simulation.Units
{
    internal partial struct Steering
    {
        /// <summary>
        /// Drives the leg, steering when the planned velocity would hit something. A planned manoeuvre isn't steered
        /// and doesn't wait: the hull room keeps it off its neighbours, and waiting on them deadlocks a mouth.
        /// </summary>
        public float2 Drive(ref Driver driver, in PathLeg leg) =>
            leg.Tight ? DriveTight(ref driver, leg) : DriveSampled(ref driver, leg);

        private static float2 DriveTight(ref Driver driver, in PathLeg leg)
        {
            var step = driver.Drive(leg);
            driver.State.LastChoice = step / driver.DeltaTime;
            return step;
        }
    }
}
