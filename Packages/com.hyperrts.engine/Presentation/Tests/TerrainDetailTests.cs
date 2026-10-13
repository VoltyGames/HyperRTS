using HyperRTS.Presentation.Environment;
using NUnit.Framework;
using UnityEngine;

namespace HyperRTS.Presentation.Tests
{
    /// <summary>The terrain detail level as global shader keywords; High sets none, like edit mode.</summary>
    public class TerrainDetailTests
    {
        [TearDown]
        public void TearDown() => TerrainDetail.Level = TerrainDetailLevel.High;

        [TestCase(TerrainDetailLevel.Low, true, false)]
        [TestCase(TerrainDetailLevel.Medium, false, true)]
        [TestCase(TerrainDetailLevel.High, false, false)]
        public void Level_SetsItsKeywords(TerrainDetailLevel level, bool simple, bool noFar)
        {
            TerrainDetail.Level = level;

            Assert.AreEqual(level, TerrainDetail.Level);
            Assert.AreEqual(simple, Shader.IsKeywordEnabled(TerrainDetail.SimpleKeyword));
            Assert.AreEqual(noFar, Shader.IsKeywordEnabled(TerrainDetail.NoFarKeyword));
        }

        [Test]
        public void LoweringThenRaising_ClearsTheKeywords()
        {
            TerrainDetail.Level = TerrainDetailLevel.Low;
            TerrainDetail.Level = TerrainDetailLevel.Medium;
            TerrainDetail.Level = TerrainDetailLevel.High;

            Assert.IsFalse(Shader.IsKeywordEnabled(TerrainDetail.SimpleKeyword));
            Assert.IsFalse(Shader.IsKeywordEnabled(TerrainDetail.NoFarKeyword));
        }
    }
}
