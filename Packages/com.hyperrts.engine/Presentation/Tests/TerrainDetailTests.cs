using System.Linq;
using HyperRTS.Presentation.Environment;
using HyperRTS.Simulation.Common;
using NUnit.Framework;

namespace HyperRTS.Presentation.Tests
{
    /// <summary>Each ground level picks its own terrain shader keyword, and Ultra none, the shader's full path.</summary>
    public class TerrainDetailTests
    {
        [Test]
        public void EveryLevelBelowUltraHasItsOwnKeyword()
        {
            var levels = new[] { GroundLevel.Low, GroundLevel.Medium, GroundLevel.High };
            var keywords = levels.Select(TerrainDetail.KeywordFor).ToArray();

            Assert.That(keywords, Is.All.Not.Null);
            Assert.That(keywords, Is.Unique);
            Assert.AreEqual(TerrainDetail.NoParallaxKeyword, TerrainDetail.KeywordFor(GroundLevel.High));
        }

        [Test]
        public void UltraSetsNoKeyword()
        {
            Assert.IsNull(TerrainDetail.KeywordFor(GroundLevel.Ultra));
        }
    }
}
