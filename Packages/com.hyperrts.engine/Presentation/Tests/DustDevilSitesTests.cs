using HyperRTS.Presentation.Environment.Ambient;
using HyperRTS.Simulation.Effects;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using Random = Unity.Mathematics.Random;

namespace HyperRTS.Presentation.Tests
{
    public class DustDevilSitesTests
    {
        private GroundMaterials _ground;

        [SetUp]
        public void SetUp()
        {
            // 20 × 20 cells of 10 m: soil on the west half, paved on the east.
            using var cells = new NativeArray<byte>(400, Allocator.Temp);
            var copy = cells;
            for (var i = 0; i < copy.Length; i++)
            {
                copy[i] = (byte)(i % 20 < 10 ? SurfaceMaterial.Soil : SurfaceMaterial.Paved);
            }

            _ground = GroundMaterials.Create(cells, new int2(20, 20), float2.zero, 10f, Allocator.Persistent);
        }

        [TearDown]
        public void TearDown() => _ground.Blob.Dispose();

        [Test]
        public void TryPick_LandsOnlyOnAllowedGround()
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
        public void TryPick_GivesUpWithoutAllowedGround()
        {
            var random = new Random(3u);

            var found = DustDevilSites.TryPick(_ground, new float2(100f, 100f), 60f,
                new[] { SurfaceMaterial.Snow }, ref random, out _);

            Assert.IsFalse(found);
        }
    }
}
