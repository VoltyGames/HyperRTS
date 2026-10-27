using Unity.Mathematics;

namespace HyperRTS.Simulation.Common
{
    /// <summary>Direction math on the XZ plane, angles counter-clockwise from x toward y.</summary>
    internal static class Planar
    {
        /// <summary>The direction a quarter turn to the left.</summary>
        public static float2 Left(float2 direction) => new(-direction.y, direction.x);

        /// <summary>The 2D cross product: positive when <paramref name="b"/> lies to the left of <paramref name="a"/>.</summary>
        public static float Cross(float2 a, float2 b) => a.x * b.y - a.y * b.x;

        /// <summary>Radians from one direction to another, positive to the left.</summary>
        public static float SignedAngle(float2 from, float2 to) => math.atan2(Cross(from, to), math.dot(from, to));

        public static float2 Rotate(float2 direction, float angle)
        {
            math.sincos(angle, out var sin, out var cos);
            return Rotate(direction, sin, cos);
        }

        public static float2 Rotate(float2 direction, float sin, float cos) =>
            new(direction.x * cos - direction.y * sin, direction.x * sin + direction.y * cos);
    }
}
