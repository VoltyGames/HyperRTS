using System;
using System.Collections.Generic;
using System.Linq;
using HyperRTS.Editor.Maps.Data;
using HyperRTS.Editor.Maps.Geometry;
using HyperRTS.Editor.Maps.Ground;
using HyperRTS.Editor.Maps.Library;
using HyperRTS.Editor.Maps.Planning;
using HyperRTS.Editor.Maps.Symmetry;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HyperRTS.Editor.Tests.Maps.Ground
{
    /// <summary>
    /// Plants and trees: grass coverage baked per plant and tile, plant files and cards, and tree variants that
    /// match across copies.
    /// </summary>
    public class MapPlantTests
    {
        // 64 m square at two texels a metre: four by four tiles of 32 texels a side.
        private static MapGrassBake Bake() => new(new Rect(-32f, -32f, 64f, 64f), 128, 2);

        [Test]
        public void Tiles_ListOnlyTilesThatGrowAPlant_WithItsMeanCoverage()
        {
            var bake = Bake();
            for (var z = 0; z < 32; z++)
            {
                for (var x = 0; x < 16; x++)
                {
                    bake.Set(1, x + 32, z, 1f);
                }
            }

            bake.Set(0, 127, 127, 1f);

            var (tiles, plants) = bake.Tiles();

            Assert.AreEqual(2, tiles.Count);
            Assert.AreEqual((1, 0), (tiles[0].x, tiles[0].z));
            Assert.AreEqual(1, tiles[0].plantCount);
            Assert.AreEqual(1, plants[tiles[0].firstPlant].plant);
            Assert.AreEqual(0.5f, plants[tiles[0].firstPlant].coverage, 1e-4f);
            Assert.AreEqual((3, 3), (tiles[1].x, tiles[1].z));
            Assert.AreEqual(0, plants[tiles[1].firstPlant].plant);
            Assert.AreEqual(1f / (32f * 32f), plants[tiles[1].firstPlant].coverage, 1e-5f);
        }

        [Test]
        public void Coverage_IsASlicePerPlant_FilteredClampedAndLinear()
        {
            var bake = Bake();
            bake.Set(0, 3, 5, 0.5f);
            bake.Set(1, 3, 5, 1f);

            var coverage = MapGrassWriter.Coverage(bake);
            try
            {
                Assert.AreEqual(2, coverage.depth);
                Assert.AreEqual(128, coverage.width);
                Assert.AreEqual(TextureFormat.R8, coverage.format);
                Assert.AreEqual(FilterMode.Bilinear, coverage.filterMode);
                Assert.AreEqual(TextureWrapMode.Clamp, coverage.wrapMode);
                Assert.AreEqual(128, coverage.GetPixelData<byte>(0, 0)[5 * 128 + 3]);
                Assert.AreEqual(255, coverage.GetPixelData<byte>(0, 1)[5 * 128 + 3]);
                Assert.AreEqual(0, coverage.GetPixelData<byte>(0, 1)[5 * 128 + 4]);
            }
            finally
            {
                Object.DestroyImmediate(coverage);
            }
        }

        [TestCase(0.1f)]
        [TestCase(0.3f)]
        [TestCase(0.75f)]
        public void Stands_CoverAboutTheirShare_InEveryCopy(float share)
        {
            var layout = MapFixture.Layout(MapFixture.Spec());
            var noise = new MapNoise(layout.Symmetry, 11, MapNoiseChannel.PlantStands);
            var (inside, samples) = (0f, 0);
            for (var x = -140f; x < 140f; x += 1.7f)
            {
                for (var z = -140f; z < 140f; z += 1.9f)
                {
                    var p = new Vector2(x, z);
                    var stand = noise.Blotches(p, 12f, share);
                    Assert.AreEqual(stand, noise.Blotches(-p, 12f, share), 1e-4f, $"stand at {p} and its copy");
                    inside += stand;
                    samples++;
                }
            }

            Assert.AreEqual(share, inside / samples, 0.08f, "stands cover about their share");
        }

        [Test]
        public void LibraryPlants_BecomeRendererPlants()
        {
            var item = MapFixture.Item<MapClimatePlant>("Grass", i =>
            {
                i.width = new Vector2(0.5f, 0.7f);
                i.height = new Vector2(0.2f, 0.3f);
                i.density = 12f;
                i.clumping = 0.4f;
            });
            var plant = item.Plant();

            Assert.AreEqual(new Vector2(0.5f, 0.7f), plant.width);
            Assert.AreEqual(new Vector2(0.2f, 0.3f), plant.height);
            Assert.AreEqual(12f, plant.density);
            Assert.AreEqual(0.4f, plant.clumping);
        }

        [Test]
        public void CardPlants_ScaleTheirMeasuredCardSize()
        {
            var plant = MapFixture.Item<MapClimatePlant>("Grass", i =>
            {
                i.width = new Vector2(0.8f, 1.2f);
                i.height = new Vector2(0.5f, 1f);
            });
            var card = new TextAsset("{\"width\": 0.5, \"height\": 0.2}");
            try
            {
                plant.card = MapPlantCard.Read(card);
                var drawn = plant.Plant();

                Assert.AreEqual(0.4f, drawn.width.x, 1e-6f);
                Assert.AreEqual(0.6f, drawn.width.y, 1e-6f);
                Assert.AreEqual(0.1f, drawn.height.x, 1e-6f);
                Assert.AreEqual(0.2f, drawn.height.y, 1e-6f);
            }
            finally
            {
                Object.DestroyImmediate(card);
            }
        }

        [TestCase("{\"width\": 0.5, \"hieght\": 0.2}", "'hieght' is not a plant card field")]
        [TestCase("{\"width\": 0.5}", "needs a width and height above 0")]
        public void PlantCards_WithATypoOrNoSize_FailNamingIt(string json, string message)
        {
            var card = new TextAsset(json);
            try
            {
                var error = Assert.Throws<InvalidOperationException>(() => MapPlantCard.Read(card));
                StringAssert.Contains(message, error.Message);
            }
            finally
            {
                Object.DestroyImmediate(card);
            }
        }

        [TestCase(MapSymmetry.Rotate180)]
        [TestCase(MapSymmetry.MirrorXName)]
        [TestCase("rotate-4")]
        public void EveryTreeCopy_GetsTheSameVariant(string symmetry)
        {
            var species = Species(3);
            var planting = new MapTreePlanting(MapSymmetry.Parse(symmetry), Climate(species), new MapTreeSpacing(null, null, null));

            for (var i = 0; i < 40; i++)
            {
                planting.PlantCopies(0, new Vector2(i * 7.3f, -i * 3.1f), 1f, 0f);
            }

            var copies = MapSymmetry.Parse(symmetry).Copies.Count;
            for (var i = 0; i < planting.Planted.Count; i += copies)
            {
                var variants = planting.Planted.Skip(i).Take(copies).Select(tree => tree.Variant);
                Assert.AreEqual(1, variants.Distinct().Count(), $"copies of tree {i / copies}");
            }
        }

        [Test]
        public void TreeSeeds_AreTheSameEveryRun()
        {
            var point = new Vector2(-123.456f, 78.9f);

            Assert.AreEqual(MapTree.SeedAt(point), MapTree.SeedAt(new Vector2(-123.456f, 78.9f)));
            Assert.AreNotEqual(MapTree.SeedAt(point), MapTree.SeedAt(point + Vector2.right));
        }

        [Test]
        public void TreeVariants_AreSpreadEvenly()
        {
            var species = Species(3);
            var counts = new int[3];
            for (var x = 0; x < 30; x++)
            {
                for (var z = 0; z < 30; z++)
                {
                    counts[species.Variant(MapTree.SeedAt(new Vector2(x * 4.5f, z * 4.5f)))]++;
                }
            }

            Assert.IsTrue(counts.All(count => count > 900 / 3 * 0.8f), $"counts {string.Join(", ", counts)}");
        }

        [Test]
        public void ATreeWithoutPrefabs_CannotPickAVariant()
        {
            var species = Species(0);

            var error = Assert.Throws<InvalidOperationException>(() => species.Variant(1u));

            StringAssert.Contains("'Oak'", error.Message);
        }

        private static MapClimateTree Species(int variants) =>
            MapFixture.Item<MapClimateTree>("Oak", i => i.prefabs = new List<GameObject>(new GameObject[variants]));

        private static MapClimate Climate(MapClimateTree species)
        {
            var climate = MapFixture.Item<MapClimate>("Test");
            climate.trees.Add(species);
            return climate;
        }
    }
}
