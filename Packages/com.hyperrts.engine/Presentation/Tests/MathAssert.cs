using NUnit.Framework;
using Unity.Mathematics;

namespace HyperRTS.Presentation.Tests
{
    /// <summary>Distance asserts for vectors, shared by this assembly's tests.</summary>
    public static class MathAssert
    {
        public static void Near(float3 expected, float3 actual, float tolerance = 1e-4f) =>
            Assert.Less(math.distance(expected, actual), tolerance, $"expected {expected}, was {actual}");

        public static void Near(float2 expected, float2 actual, float tolerance = 1e-4f) =>
            Near(new float3(expected, 0f), new float3(actual, 0f), tolerance);
    }
}
