using Unity.Mathematics;

namespace HyperRTS.Simulation.Common
{
    /// <summary>A level heading in one byte, 256 steps from +Z toward +X, for compact network and simulation data.</summary>
    public static class HeadingByte
    {
        public const int Steps = 256;

        /// <summary>The nearest step to the xz direction; a zero vector gives 0.</summary>
        public static byte Encode(float2 xz)
        {
            var turns = math.atan2(xz.x, xz.y) / (2f * math.PI);
            return (byte)((int)math.round(turns * Steps) & (Steps - 1));
        }

        /// <summary>Radians from +Z toward +X.</summary>
        public static float Radians(byte heading) => heading * (2f * math.PI / Steps);

        public static float3 Direction(byte heading)
        {
            math.sincos(Radians(heading), out var sin, out var cos);
            return new float3(sin, 0f, cos);
        }
    }
}
