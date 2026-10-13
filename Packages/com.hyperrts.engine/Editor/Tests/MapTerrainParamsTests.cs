using HyperRTS.Editor.Maps.Data;
using HyperRTS.Editor.Maps.Texturing;
using NUnit.Framework;
using UnityEngine;

namespace HyperRTS.Editor.Tests
{
    /// <summary>Terrain parameter row 3: hex rotation strength and whether hex offsets may cross rows.</summary>
    public class MapTerrainParamsTests
    {
        private TerrainLayer _terrainLayer;

        [SetUp]
        public void SetUp() => _terrainLayer = new TerrainLayer { normalScale = 0.8f };

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_terrainLayer);

        private MapClimateLayer Layer(bool followsRows) => new()
        {
            terrainLayer = _terrainLayer,
            followsRows = followsRows,
            heightBlend = 1.5f,
            antiTiling = 0.6f,
        };

        [Test]
        public void FreeLayer_TurnsByItsAntiTiling_AndShiftsAcrossRows()
        {
            var surface = MapTerrainParams.Surface(Layer(false));

            Assert.AreEqual(1.5f, surface.r, 1e-5f);
            Assert.AreEqual(0.8f, surface.g, 1e-5f);
            Assert.AreEqual(0.6f, surface.b, 1e-5f, "rotation strength");
            Assert.AreEqual(1f, surface.a, "offsets may cross rows");
        }

        [Test]
        public void RowLayer_NeverTurns_AndShiftsOnlyAlongRows()
        {
            var surface = MapTerrainParams.Surface(Layer(true));

            Assert.AreEqual(0f, surface.b, "rotation strength");
            Assert.AreEqual(0f, surface.a, "offsets may cross rows");
        }
    }
}
