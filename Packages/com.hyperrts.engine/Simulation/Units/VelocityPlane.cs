using Unity.Mathematics;

namespace HyperRTS.Simulation.Units
{
    /// <summary>An ORCA half-plane of velocities: those left of <see cref="Direction"/> through <see cref="Point"/>.</summary>
    internal struct VelocityPlane
    {
        public float2 Point;
        public float2 Direction;

        /// <summary>How far <paramref name="velocity"/> lies outside the plane; zero or less inside it.</summary>
        public readonly float Violation(float2 velocity) => Cross(Direction, Point - velocity);

        public static float Cross(float2 a, float2 b) => a.x * b.y - a.y * b.x;
    }
}
