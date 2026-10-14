using HyperRTS.Simulation.Effects;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;

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
    }
}
