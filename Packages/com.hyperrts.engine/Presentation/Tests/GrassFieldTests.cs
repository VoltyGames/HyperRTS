using System.Collections.Generic;
using HyperRTS.Presentation.Environment.Grass;
using NUnit.Framework;
using UnityEngine;

namespace HyperRTS.Presentation.Tests
{
    /// <summary>A grass field lays its plants' draw slots end to end and names what keeps it from drawing.</summary>
    public class GrassFieldTests
    {
        private GrassField _field;
        private Mesh _mesh;
        private Material _material;
        private Texture2DArray _coverage;

        [SetUp]
        public void SetUp()
        {
            _mesh = new Mesh();
            _mesh.SetVertices(new[] { Vector3.zero, Vector3.up * 0.8f, Vector3.right });
            _mesh.SetTriangles(new[] { 0, 1, 2 }, 0);
            _material = new Material(Shader.Find("Hidden/HyperRTS/FoliageGrass"));
            _coverage = new Texture2DArray(4, 4, 2, TextureFormat.R8, false, true);
            _field = ScriptableObject.CreateInstance<GrassField>();
            _field.coverage = _coverage;
            _field.area = new Rect(-32f, -32f, 64f, 64f);
            _field.plants = new List<GrassPlant> { Plant("Grass", 2, 9f), Plant("Thistle", 3, 0.25f) };
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_field);
            Object.DestroyImmediate(_coverage);
            Object.DestroyImmediate(_material);
            Object.DestroyImmediate(_mesh);
        }

        [Test]
        public void Slots_RunPlantByPlant_AModelPerBandEach()
        {
            var sowings = _field.Sowings();

            Assert.AreEqual((2 + 3) * GrassView.Bands, _field.SlotCount);
            Assert.AreEqual(0, sowings[0].firstSlot);
            Assert.AreEqual(2 * GrassView.Bands, sowings[1].firstSlot);
            Assert.AreEqual(3, sowings[1].variants);
            Assert.AreNotEqual(sowings[0].seed, sowings[1].seed);
        }

        [Test]
        public void Sowing_SpacesCellsForTheDensity()
        {
            Assert.AreEqual(1f / 3f, _field.Sowings()[0].spacing, 1e-5f);
            Assert.AreEqual(2f, _field.plants[1].Spacing, 1e-5f);
            Assert.AreEqual(0.8f * 1.2f, _field.plants[0].Tallest, 1e-5f);
        }

        [Test]
        public void TileRects_StepFromTheAreasCorner()
        {
            var rect = _field.TileRect(new GrassTile { x = 2, z = 1 });

            Assert.AreEqual(new Rect(-32f + 2f * GrassField.TileSize, -32f + GrassField.TileSize, 16f, 16f), rect);
        }

        [Test]
        public void Problems_NameMissingModelsAndSlicesThatDontMatchThePlants()
        {
            CollectionAssert.IsEmpty(_field.Problems());

            _field.plants[1].meshes.Clear();
            _field.plants.Add(Plant("Yarrow", 1, 0f));

            var problems = string.Join("\n", _field.Problems());
            StringAssert.Contains("coverage has 2 slices for 3 plants", problems);
            StringAssert.Contains("plant 'Thistle': meshes is empty", problems);
            StringAssert.Contains("plant 'Yarrow': density 0 must be above 0", problems);
        }

        private GrassPlant Plant(string name, int models, float density)
        {
            var plant = new GrassPlant
            {
                name = name,
                material = _material,
                width = new Vector2(0.8f, 1.2f),
                height = new Vector2(0.8f, 1.2f),
                density = density,
            };
            for (var i = 0; i < models; i++)
            {
                plant.meshes.Add(_mesh);
            }

            return plant;
        }
    }
}
