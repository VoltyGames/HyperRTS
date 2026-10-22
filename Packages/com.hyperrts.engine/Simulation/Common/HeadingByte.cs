using Unity.Mathematics;

namespace HyperRTS.Simulation.Common
{
    /// <summary>A level heading in one byte, 256 steps from +Z toward +X, for compact network and simulation data.</summary>
    public static class HeadingByte
    {
        public const int Steps = 256;

        /// <summary>The nearest step to the xz direction; a zero vector gives 0.</summary>
        public static byte Encode(float2 xz) => Encode(math.atan2(xz.x, xz.y));

        /// <summary>The nearest step to an angle in radians from +Z toward +X, any number of turns.</summary>
        public static byte Encode(float radians)
        {
            var turns = radians / (2f * math.PI);
            return (byte)((int)math.round(turns * Steps) & (Steps - 1));
        }

        /// <summary>Radians from +Z toward +X.</summary>
        public static float Radians(byte heading) => heading * (2f * math.PI / Steps);

        /// <summary>The same angle brought into -pi to pi.</summary>
        public static float Wrap(float radians) => radians - 2f * math.PI * math.round(radians / (2f * math.PI));

        public static float3 Direction(byte heading)
        {
            math.sincos(Radians(heading), out var sin, out var cos);
            return new float3(sin, 0f, cos);
        }
    }
}
