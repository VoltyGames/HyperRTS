using HyperRTS.Simulation.Units;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Tests
{
    /// <summary>The tracked and wheeled handling the drive and locomotion tests share.</summary>
    public static class TestVehicles
    {
        public static readonly Locomotion Tracks = new()
        {
            Acceleration = 2.5f, Braking = 6f, TurnRate = math.radians(50f), ReverseSpeed = 3f,
        };

        public static readonly Locomotion Wheels = new()
        {
            Acceleration = 3f, Braking = 6f, TurnRate = math.radians(40f), SteerRate = math.radians(45f),
            ReverseSpeed = 3f,
        };
    }
}
