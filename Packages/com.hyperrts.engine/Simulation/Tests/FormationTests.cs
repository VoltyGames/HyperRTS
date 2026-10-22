using HyperRTS.Simulation.Commands;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Tests
{
    /// <summary>Formation slots sized to each unit in a mixed group.</summary>
    public class FormationTests
    {
        [Test]
        public void MixedRow_GivesEachUnitASlotItsOwnWidth()
        {
            using var positions = new NativeArray<float3>(new[]
            {
                new float3(-2f, 0f, 0f), new float3(0f, 0f, 0f), new float3(2f, 0f, 0f),
            }, Allocator.Temp);
            using var spacings = new NativeArray<float2>(new[]
            {
                new float2(5f, 8f), new float2(1f, 1f), new float2(1f, 1f),
            }, Allocator.Temp);
            using var slots = new NativeArray<float3>(3, Allocator.Temp);

            Formation.Assign(positions, new float3(0f, 0f, 10f), spacings, slots);

            Assert.AreEqual(1f, math.distance(slots[1], slots[2]), 1e-4f, "infantry stand an infantry width apart");
            Assert.AreEqual(3f, math.distance(slots[0], slots[1]), 1e-4f, "the tank keeps half its own width");
            Assert.AreEqual(10f, slots[0].z, 1e-4f, "one row on the goal");
            Assert.Less(slots[0].x, slots[1].x, "units keep their places left to right");
        }
    }
}
