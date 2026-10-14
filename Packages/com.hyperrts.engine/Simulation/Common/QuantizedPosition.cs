using Unity.Mathematics;

namespace HyperRTS.Simulation.Common
{
    /// <summary>World coordinates in 16 bits each, for compact replay and network data.</summary>
    public static class QuantizedPosition
    {
        /// <summary>Metres per step; 16-bit coordinates cover ±2 km.</summary>
        public const float Step = 1f / 16f;

        /// <summary>One world coordinate in <see cref="Step"/>s, clamped to 16 bits.</summary>
        public static short Quantize(float metres) =>
            (short)math.clamp((int)math.round(metres / Step), short.MinValue, short.MaxValue);

        public static float3 Dequantize(short x, short y, short z) => new float3(x, y, z) * Step;
    }
}
