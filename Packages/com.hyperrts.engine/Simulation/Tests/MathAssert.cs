using NUnit.Framework;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Tests
{
    /// <summary>Distance asserts for vectors and matrices, shared by the simulation and presentation tests.</summary>
    public static class MathAssert
    {
        public static void Near(float3 expected, float3 actual, float tolerance = 1e-4f) =>
            Assert.Less(math.distance(expected, actual), tolerance, $"expected {expected}, was {actual}");

        public static void Near(float2 expected, float2 actual, float tolerance = 1e-4f) =>
            Near(new float3(expected, 0f), new float3(actual, 0f), tolerance);

        /// <summary>Column by column, so a failure names the column.</summary>
        public static void Near(float4x4 expected, float4x4 actual, float tolerance = 1e-4f)
        {
            for (var c = 0; c < 4; c++)
            {
                Assert.Less(math.distance(expected[c], actual[c]), tolerance,
                    $"column {c}: expected {expected[c]}, was {actual[c]}");
            }
        }
    }
}
