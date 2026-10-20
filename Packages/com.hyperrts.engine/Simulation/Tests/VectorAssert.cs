using NUnit.Framework;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Tests
{
    /// <summary>Vector asserts NUnit lacks, shared by the math and pose tests.</summary>
    public static class VectorAssert
    {
        public static void AssertNear(float3 expected, float3 actual, float tolerance) =>
            Assert.Less(math.distance(expected, actual), tolerance, $"expected {expected}, was {actual}");
    }
}
