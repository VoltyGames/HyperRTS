using HyperRTS.Editor.Maps.Data;
using HyperRTS.Editor.Maps.Texturing;
using NUnit.Framework;
using UnityEngine;

namespace HyperRTS.Editor.Tests
{
    /// <summary>Terrain parameter rows 0 and 3: the layer turn, hex rotation strength and the follows-rows flag.</summary>
    public class MapTerrainParamsTests
    {
        private TerrainLayer _terrainLayer;

        [SetUp]
        public void SetUp() => _terrainLayer = new TerrainLayer { normalScale = 0.8f, tileSize = new Vector2(4f, 2f) };

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_terrainLayer);

        private MapClimateLayer Layer(bool followsRows) => new()
        {
            terrainLayer = _terrainLayer,
            followsRows = followsRows,
            heightBlend = 1.5f,
            antiTiling = 0.6f,
            rotation = 90f,
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
            Assert.AreEqual(0f, surface.a, "flagged as following rows");
        }

        [Test]
        public void RowLayer_MapsByItsOwnTurnOnly_TheRowMapAddsTheRest()
        {
            var mapping = MapTerrainParams.Mapping(Layer(true));

            Assert.AreEqual(0f, mapping.r, 1e-5f, "cos 90°");
            Assert.AreEqual(1f, mapping.g, 1e-5f, "sin 90°");
            Assert.AreEqual(0.25f, mapping.b, 1e-5f);
            Assert.AreEqual(0.5f, mapping.a, 1e-5f);
        }
    }
}
