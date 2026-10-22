using System;
using HyperRTS.Editor.Maps.Data;
using HyperRTS.Editor.Maps.Ground;
using HyperRTS.Editor.Maps.Library;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HyperRTS.Editor.Tests
{
    /// <summary>
    /// Painted grass coverage becomes a texture array slice per plant and a tile summary of the plants each tile
    /// grows, and plant files name their models, material and card size.
    /// </summary>
    public class MapGrassBakeTests
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

        [Test]
        public void PlantFiles_NameTheirModelsAndMaterial_AndBecomeRendererPlants()
        {
            const string json = "{\"version\": 1, \"meshes\": [], \"material\": {}, \"width\": {\"x\": 0.5, \"y\": 0.7}, " +
                                "\"height\": {\"x\": 0.2, \"y\": 0.3}, \"density\": 12, \"clumping\": 0.4}";

            var (item, _) = MapLibraryItemFile.Parse(typeof(MapClimatePlant), json, "Grass.plant.json");
            var plant = ((MapClimatePlant)item).Plant();

            Assert.AreEqual(new Vector2(0.5f, 0.7f), plant.width);
            Assert.AreEqual(new Vector2(0.2f, 0.3f), plant.height);
            Assert.AreEqual(12f, plant.density);
            Assert.AreEqual(0.4f, plant.clumping);
        }

        [Test]
        public void CardPlants_ScaleTheirMeasuredCardSize()
        {
            const string json = "{\"version\": 1, \"width\": {\"x\": 0.8, \"y\": 1.2}, \"height\": {\"x\": 0.5, \"y\": 1}}";
            var (item, _) = MapLibraryItemFile.Parse(typeof(MapClimatePlant), json, "Grass.plant.json");
            var plant = (MapClimatePlant)item;
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

        [Test]
        public void PlantFiles_InTheOldPrefabFormat_AreRejectedByName()
        {
            const string json = "{\"version\": 1, \"mesh\": \"Assets/Detail_Grass.prefab\", \"texture\": \"\", \"density\": 2}";

            var error = Assert.Throws<InvalidOperationException>(() =>
                MapLibraryItemFile.Parse(typeof(MapClimatePlant), json, "Grass.plant.json"));

            StringAssert.Contains("'mesh' is not a plant field", error.Message);
        }
    }
}
