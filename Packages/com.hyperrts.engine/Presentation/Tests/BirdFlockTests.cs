using System;
using HyperRTS.Presentation.Environment.Ambient;
using HyperRTS.Simulation.Navigation;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;

namespace HyperRTS.Presentation.Tests
{
    public class BirdFlockTests
    {
        private const float Step = 0.05f;

        private static readonly BirdSettings Settings = new()
        {
            Height = new float2(20f, 30f),
            ScatterRadius = 50f,
            ReturnDelay = new float2(4f, 4f),
        };

        private TerrainHeight _terrain;
        private SkyArea _area;

        [SetUp]
        public void SetUp()
        {
            using var heights = new NativeArray<float>(41 * 41, Allocator.Temp);
            _terrain = TerrainHeight.Create(heights, new int2(41, 41), float2.zero, 20f, Allocator.Persistent);
            _area = new SkyArea(_terrain);
        }

        [TearDown]
        public void TearDown() => _terrain.Blob.Dispose();

        [Test]
        public void Circling_StaysNearTheAnchorAtFlyingHeight()
        {
            var flock = new BirdFlock(12, Settings, _area, 7u);
            Run(flock, 20f, ReadOnlySpan<float3>.Empty);

            for (var bird = 0; bird < flock.Count; bird++)
            {
                var position = flock.Position(bird);
                Assert.Less(math.distance(position.xz, flock.Anchor.xz), 45f);
                Assert.That(position.y, Is.InRange(12f, 38f));
            }

            Assert.IsFalse(flock.Scattered);
        }

        [Test]
        public void NearbyImpact_ScattersTheFlockAwayAndUp()
        {
            var flock = new BirdFlock(12, Settings, _area, 7u);
            Run(flock, 5f, ReadOnlySpan<float3>.Empty);
            var center = flock.Center;
            var impact = new float3(center.x + 45f, 0f, center.z);

            flock.Step(Step, new[] { impact });
            Run(flock, 2f, ReadOnlySpan<float3>.Empty);

            Assert.IsTrue(flock.Scattered);
            Assert.Less(flock.Center.x, center.x - 15f, "flies away from the impact");
            Assert.Greater(flock.Center.y, center.y, "climbs");
        }

        [Test]
        public void FarImpact_IsIgnored()
        {
            var flock = new BirdFlock(12, Settings, _area, 7u);
            var center = flock.Center;

            flock.Step(Step, new[] { new float3(center.x + 200f, 0f, center.z) });

            Assert.IsFalse(flock.Scattered);
        }

        [Test]
        public void AfterTheDelay_SettlesOverADistantAnchor()
        {
            var flock = new BirdFlock(12, Settings, _area, 7u);
            var anchor = flock.Anchor;

            flock.Step(Step, new[] { flock.Center });
            Run(flock, 4.5f, ReadOnlySpan<float3>.Empty);

            Assert.IsFalse(flock.Scattered);
            Assert.GreaterOrEqual(math.distance(flock.Anchor.xz, anchor.xz), BirdFlock.ResettleDistance);
        }

        private static void Run(BirdFlock flock, float seconds, ReadOnlySpan<float3> impacts)
        {
            for (var time = 0f; time < seconds; time += Step)
            {
                flock.Step(Step, impacts);
            }
        }
    }
}
