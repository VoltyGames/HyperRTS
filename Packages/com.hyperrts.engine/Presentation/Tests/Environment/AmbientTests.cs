using System;
using HyperRTS.Presentation.Environment.Ambient;
using HyperRTS.Simulation.Cues;
using HyperRTS.Simulation.Navigation;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using Random = Unity.Mathematics.Random;

namespace HyperRTS.Presentation.Tests.Environment
{
    /// <summary>Ambient life: bird flocks circling and scattering from impacts, and where dust devils may spin up.</summary>
    public class AmbientTests
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
        private GroundMaterials _ground;

        [SetUp]
        public void SetUp()
        {
            using var heights = new NativeArray<float>(41 * 41, Allocator.Temp);
            _terrain = TerrainHeight.Create(heights, new int2(41, 41), float2.zero, 20f, Allocator.Persistent);
            _area = new SkyArea(_terrain);

            // 20 by 20 cells of 10 m: soil on the west half, paved on the east.
            using var cells = new NativeArray<byte>(400, Allocator.Temp);
            var copy = cells;
            for (var i = 0; i < copy.Length; i++)
            {
                copy[i] = (byte)(i % 20 < 10 ? SurfaceMaterial.Soil : SurfaceMaterial.Paved);
            }

            _ground = GroundMaterials.Create(cells, new int2(20, 20), float2.zero, 10f, Allocator.Persistent);
        }

        [TearDown]
        public void TearDown()
        {
            _terrain.Blob.Dispose();
            _ground.Blob.Dispose();
        }

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
        public void NearbyImpact_ScattersTheFlockAwayAndUp_AFarOneIsIgnored()
        {
            var flock = new BirdFlock(12, Settings, _area, 7u);
            Run(flock, 5f, ReadOnlySpan<float3>.Empty);
            var center = flock.Center;

            flock.Step(Step, new[] { new float3(center.x + 200f, 0f, center.z) });
            Assert.IsFalse(flock.Scattered, "far impact");

            flock.Step(Step, new[] { new float3(center.x + 45f, 0f, center.z) });
            Run(flock, 2f, ReadOnlySpan<float3>.Empty);

            Assert.IsTrue(flock.Scattered);
            Assert.Less(flock.Center.x, center.x - 15f, "flies away from the impact");
            Assert.Greater(flock.Center.y, center.y, "climbs");
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

        [Test]
        public void DustDevil_LandsOnlyOnAllowedGround()
        {
            var random = new Random(3u);
            var allowed = new[] { SurfaceMaterial.Soil };
            for (var pick = 0; pick < 50; pick++)
            {
                Assert.IsTrue(DustDevilSites.TryPick(_ground, new float2(100f, 100f), 60f, allowed,
                    ref random, out var site));
                Assert.AreEqual(SurfaceMaterial.Soil, _ground.At(site));
                Assert.LessOrEqual(math.distance(site, new float2(100f, 100f)), 60f);
            }
        }

        [Test]
        public void DustDevil_GivesUpWithoutAllowedGround()
        {
            var random = new Random(3u);

            var found = DustDevilSites.TryPick(_ground, new float2(100f, 100f), 60f,
                new[] { SurfaceMaterial.Snow }, ref random, out _);

            Assert.IsFalse(found);
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
