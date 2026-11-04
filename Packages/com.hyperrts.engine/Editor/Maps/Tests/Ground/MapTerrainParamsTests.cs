using HyperRTS.Editor.Maps.Climate;
using HyperRTS.Editor.Maps.Texturing;
using NUnit.Framework;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Tests.Ground
{
    /// <summary>Each climate layer's terrain parameters: surface turn and shift, mapping, grass and slope.</summary>
    public class MapTerrainParamsTests
    {
        private TerrainLayer _terrainLayer;

        [SetUp]
        public void SetUp() => _terrainLayer = new TerrainLayer { normalScale = 0.8f, tileSize = new Vector2(4f, 2f) };

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_terrainLayer);

        [Test]
        public void TerrainParams_AFreeLayerTurnsAndShiftsAcrossRows_ARowLayerMapsByItsOwnTurnOnly()
        {
            var free = MapTerrainParams.Surface(Layer(false));
            var rows = MapTerrainParams.Surface(Layer(true));
            var mapping = MapTerrainParams.Mapping(Layer(true));

            Assert.AreEqual(1.5f, free.r, 1e-5f);
            Assert.AreEqual(0.8f, free.g, 1e-5f);
            Assert.AreEqual(0.6f, free.b, 1e-5f, "rotation strength");
            Assert.AreEqual(1f, free.a, "offsets may cross rows");
            Assert.AreEqual(0f, rows.b, "a row layer never turns");
            Assert.AreEqual(0f, rows.a, "flagged as following rows");
            Assert.AreEqual(0f, mapping.r, 1e-5f, "cos 90");
            Assert.AreEqual(1f, mapping.g, 1e-5f, "sin 90");
            Assert.AreEqual(0.25f, mapping.b, 1e-5f);
            Assert.AreEqual(0.5f, mapping.a, 1e-5f);
        }

        [Test]
        public void TerrainParams_GrassAndSlope_IsTheAuthoredGrass_AndMarksOnlyTheSlopeLayerTriplanar()
        {
            Assert.AreEqual(0.4f, MapTerrainParams.GrassAndSlope(Layer(false), false).r, 1e-5f);
            Assert.AreEqual(1f, MapTerrainParams.GrassAndSlope(Layer(false), true).g, "the slope layer");
            Assert.AreEqual(0f, MapTerrainParams.GrassAndSlope(Layer(false), false).g, "any other layer");
        }

        private MapClimateLayer Layer(bool followsRows) => MapFixture.Item<MapClimateLayer>("Layer", i =>
        {
            i.terrainLayer = _terrainLayer;
            i.followsRows = followsRows;
            i.heightBlend = 1.5f;
            i.antiTiling = 0.6f;
            i.rotation = 90f;
            i.grass = 0.4f;
        });
    }
}
