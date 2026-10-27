using Unity.Mathematics;

namespace HyperRTS.Simulation.Units
{
    internal partial struct Steering
    {
        /// <summary>
        /// Drives the leg, steering when the planned velocity would hit something. A planned manoeuvre isn't steered
        /// and doesn't wait: the hull room keeps it off its neighbours, and waiting on them deadlocks a mouth.
        /// </summary>
        public float2 Drive(in Locomotion locomotion, ref LocomotionState drive, float2 facing, ref float2 heading,
            in PathLeg leg, float deltaTime) =>
            leg.Tight
                ? DriveTight(locomotion, ref drive, facing, ref heading, leg, deltaTime)
                : DriveSampled(locomotion, ref drive, facing, ref heading, leg, deltaTime);

        private readonly float2 DriveTight(in Locomotion locomotion, ref LocomotionState drive, float2 facing,
            ref float2 heading, in PathLeg leg, float deltaTime)
        {
            heading = facing;
            var step = locomotion.Drive(ref drive, _chassis, _position, ref heading, leg, _topSpeed, _radius, deltaTime);
            drive.LastChoice = step / deltaTime;
            return step;
        }
    }
}
