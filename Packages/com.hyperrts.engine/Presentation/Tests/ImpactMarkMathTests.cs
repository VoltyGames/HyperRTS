using HyperRTS.Presentation.Impacts;
using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Navigation;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace HyperRTS.Presentation.Tests
{
    /// <summary>Impact mark bookkeeping and placement: the ring, fades, sizes and the ground frame.</summary>
    public class ImpactMarkMathTests
    {
        [Test]
        public void Ring_FillsInOrder_ThenReusesTheOldest()
        {
            var ring = new ImpactMarkRing(3);

            Assert.AreEqual(0, ring.Place(1f));
            Assert.AreEqual(1, ring.Place(2f));
            Assert.AreEqual(2, ring.Place(3f));
            Assert.AreEqual(3, ring.Count);
            Assert.AreEqual(0, ring.Place(4f), "past the cap the oldest slot goes");
            Assert.AreEqual(1f, ring.Age(0, 5f), 1e-5f);
            Assert.AreEqual(3, ring.Count);

            ring.Clear();
            Assert.AreEqual(0, ring.Count);
            Assert.AreEqual(0, ring.Place(6f));
        }

        [Test]
        public void Strength_HoldsForTheLifetime_FadesOut_AndLastsWithoutOne()
        {
            Assert.AreEqual(1f, ImpactMarkMath.Strength(10f, 10f, 4f));
            Assert.AreEqual(0.5f, ImpactMarkMath.Strength(12f, 10f, 4f), 1e-5f);
            Assert.AreEqual(0f, ImpactMarkMath.Strength(15f, 10f, 4f));
            Assert.AreEqual(0f, ImpactMarkMath.Strength(10.1f, 10f, 0f), "no fade time: gone at once");
            Assert.AreEqual(1f, ImpactMarkMath.Strength(1e6f, 0f, 4f), "no lifetime: kept until reused");
        }

        [Test]
        public void Diameter_SpansTheRange_AndGrowsWithTheSplash()
        {
            var mark = ScriptableObject.CreateInstance<ImpactMark>();
            mark.size = new Vector2(2f, 4f);
            mark.sizePerSplashRadius = 0.5f;

            Assert.AreEqual(2f, ImpactMarkMath.Diameter(mark, 0f, 0f), 1e-5f);
            Assert.AreEqual(4f, ImpactMarkMath.Diameter(mark, 0f, 1f), 1e-5f);
            Assert.AreEqual(6f, ImpactMarkMath.Diameter(mark, 6f, 0.5f), 1e-5f);
            Object.DestroyImmediate(mark);
        }

        [Test]
        public void GroundFrame_FollowsTheSlope_AndProjectsIntoIt()
        {
            // A 45° ramp rising along x: height = x.
            using var heights = new NativeArray<float>(new[] { 0f, 10f, 0f, 10f }, Allocator.Temp);
            var terrain = TerrainHeight.Create(heights, new int2(2, 2), new float2(0f, 0f), 10f, Allocator.Persistent);
            try
            {
                var normal = ImpactMarkMath.GroundNormal(terrain, new float2(5f, 5f), 1f);
                Assert.That(math.distance(math.normalize(new float3(-1f, 1f, 0f)), normal), Is.LessThan(1e-4f));

                var facing = ImpactMarkMath.Facing(normal, 0.7f);
                var forward = math.mul(facing, new float3(0f, 0f, 1f));
                var up = math.mul(facing, new float3(0f, 1f, 0f));
                Assert.That(math.distance(-normal, forward), Is.LessThan(1e-4f), "projects into the ground");
                Assert.AreEqual(0f, math.dot(up, normal), 1e-4f, "the decal lies in the ground's plane");
            }
            finally
            {
                terrain.Blob.Dispose();
            }
        }

        [Test]
        public void Facing_OnFlatGround_ProjectsStraightDown_TurnedByTheYaw()
        {
            var facing = ImpactMarkMath.Facing(new float3(0f, 1f, 0f), math.PI / 2f);
            var forward = math.mul(facing, new float3(0f, 0f, 1f));
            var up = math.mul(facing, new float3(0f, 1f, 0f));

            Assert.That(math.distance(new float3(0f, -1f, 0f), forward), Is.LessThan(1e-4f));
            Assert.That(math.distance(new float3(1f, 0f, 0f), up), Is.LessThan(1e-4f), "a quarter turn from north");
        }
    }
}
