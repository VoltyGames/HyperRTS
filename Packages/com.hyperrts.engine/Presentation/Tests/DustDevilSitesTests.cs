using System.Collections.Generic;
using HyperRTS.Presentation.Environment.Ambient;
using HyperRTS.Presentation.Environment.RowCrops;
using HyperRTS.Simulation.Effects;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
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
                Assert.IsTrue(DustDevilSites.TryPick(_ground, RowCropCover.None, new float2(100f, 100f), 60f, allowed,
                    ref random, out var site));
                Assert.AreEqual(SurfaceMaterial.Soil, _ground.At(site));
                Assert.LessOrEqual(math.distance(site, new float2(100f, 100f)), 60f);
            }
        }

        [Test]
        public void TryPick_GivesUpWithoutAllowedGround()
        {
            var random = new Random(3u);

            var found = DustDevilSites.TryPick(_ground, RowCropCover.None, new float2(100f, 100f), 60f,
                new[] { SurfaceMaterial.Snow }, ref random, out _);

            Assert.IsFalse(found);
        }

        [Test]
        public void TryPick_KeepsOffRowCrops()
        {
            // Crops sow the soil from x 48 to 80 (tiles 1 and 2 of a field area starting at x 32).
            var crops = Cover(new Rect(32f, 0f, 64f, 200f), 1, 2);
            var random = new Random(3u);
            var allowed = new[] { SurfaceMaterial.Soil };
            var found = 0;
            for (var pick = 0; pick < 50; pick++)
            {
                if (!DustDevilSites.TryPick(_ground, crops, new float2(60f, 100f), 60f, allowed, ref random,
                        out var site))
                {
                    continue;
                }

                found++;
                Assert.IsFalse(crops.Covers(site), $"picked {site} inside the crops");
                Assert.AreEqual(SurfaceMaterial.Soil, _ground.At(site));
            }

            Assert.Greater(found, 0);
        }

        [Test]
        public void TryPick_GivesUpWhenCropsCoverAllSoil()
        {
            var crops = Cover(new Rect(0f, 0f, 112f, 208f), 0, 1, 2, 3, 4, 5, 6);
            var random = new Random(3u);

            var found = DustDevilSites.TryPick(_ground, crops, new float2(50f, 100f), 40f,
                new[] { SurfaceMaterial.Soil }, ref random, out _);

            Assert.IsFalse(found);
        }

        [Test]
        public void Covers_OnlySownTiles()
        {
            var crops = Cover(new Rect(32f, 0f, 64f, 200f), 1);

            Assert.IsTrue(crops.Covers(new float2(50f, 5f)));
            Assert.IsFalse(crops.Covers(new float2(40f, 5f)));
            Assert.IsFalse(crops.Covers(new float2(70f, 5f)));
            Assert.IsFalse(crops.Covers(new float2(10f, 5f)));
        }

        /// <summary>Fields over <paramref name="area"/> sown in every row of the given tile columns.</summary>
        private static RowCropCover Cover(Rect area, params int[] columns)
        {
            var fields = ScriptableObject.CreateInstance<RowCropFields>();
            fields.area = area;
            fields.tiles = new List<RowCropTile>();
            var rows = (int)math.ceil(area.height / RowCropFields.TileSize);
            foreach (var column in columns)
            {
                for (var row = 0; row < rows; row++)
                {
                    fields.tiles.Add(new RowCropTile { x = column, z = row, fieldCount = 1 });
                }
            }

            var cover = RowCropCover.Of(fields);
            Object.DestroyImmediate(fields);
            return cover;
        }
    }
}
