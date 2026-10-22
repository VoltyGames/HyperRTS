using System;
using HyperRTS.Simulation.Cues;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HyperRTS.Simulation.Tests
{
    public class GroundMaterialsTests
    {
        private GroundMaterials _materials;

        /// <summary>A 3 × 2 map of 2 m cells from (10, 20): south row Soil, Grass, Mud; north row Sand, Gravel, Water.</summary>
        [SetUp]
        public void SetUp()
        {
            var cells = new NativeArray<byte>(new[]
            {
                (byte)SurfaceMaterial.Soil, (byte)SurfaceMaterial.Grass, (byte)SurfaceMaterial.Mud,
                (byte)SurfaceMaterial.Sand, (byte)SurfaceMaterial.Gravel, (byte)SurfaceMaterial.Water,
            }, Allocator.Temp);
            _materials = GroundMaterials.Create(cells, new int2(3, 2), new float2(10f, 20f), 2f, Allocator.Persistent);
            cells.Dispose();
        }

        [TearDown]
        public void TearDown() => _materials.Blob.Dispose();

        [TestCase(10.1f, 20.1f, SurfaceMaterial.Soil)]
        [TestCase(13f, 21f, SurfaceMaterial.Grass)]
        [TestCase(15.9f, 23.9f, SurfaceMaterial.Water)]
        [TestCase(12f, 22f, SurfaceMaterial.Gravel)]
        public void At_ReturnsTheCellUnderThePoint(float x, float z, SurfaceMaterial expected) =>
            Assert.AreEqual(expected, _materials.At(new float2(x, z)));

        [TestCase(9.9f, 21f)]
        [TestCase(16f, 21f)]
        [TestCase(12f, 19.9f)]
        [TestCase(12f, 24f)]
        public void At_IsNoneOffTheMap(float x, float z) =>
            Assert.AreEqual(SurfaceMaterial.None, _materials.At(new float2(x, z)));

        [Test]
        public void MapAsset_BakesItsCellsAtTheCorner()
        {
            var map = Map(2, 1, (byte)SurfaceMaterial.Mud, (byte)SurfaceMaterial.Paved);
            var baked = map.Bake(new float2(-4f, 0f), Allocator.Persistent);

            Assert.AreEqual(SurfaceMaterial.Mud, baked.At(new float2(-3f, 1f)));
            Assert.AreEqual(SurfaceMaterial.Paved, baked.At(new float2(-1f, 1f)));
            Assert.AreEqual(SurfaceMaterial.None, baked.At(new float2(0.5f, 1f)));
            baked.Blob.Dispose();
            Object.DestroyImmediate(map);
        }

        [Test]
        public void MapAsset_WithTheWrongCellCount_IsAnError()
        {
            var map = Map(3, 2, (byte)SurfaceMaterial.Soil);
            var error = Assert.Throws<InvalidOperationException>(() => map.Bake(float2.zero, Allocator.Persistent));
            StringAssert.Contains("holds 1 cells, not 3 x 2", error.Message);
            Object.DestroyImmediate(map);
        }

        private static GroundMaterialMap Map(int width, int height, params byte[] cells)
        {
            var map = ScriptableObject.CreateInstance<GroundMaterialMap>();
            (map.width, map.height, map.spacing, map.cells) = (width, height, 2f, cells);
            return map;
        }
    }
}
