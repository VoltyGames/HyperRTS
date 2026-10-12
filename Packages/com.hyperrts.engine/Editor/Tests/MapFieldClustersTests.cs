using HyperRTS.Editor.Maps.Report;
using NUnit.Framework;
using UnityEngine;

namespace HyperRTS.Editor.Tests
{
    /// <summary>How the map report groups resource stacks into fields.</summary>
    public class MapFieldClustersTests
    {
        [Test]
        public void NoStacksAreNoFields()
        {
            Assert.AreEqual(0, MapFieldClusters.Count(new Vector2[0]));
        }

        [Test]
        public void ClosePairIsOneField()
        {
            var stacks = new[] { new Vector2(0f, 0f), new Vector2(8f, 6f) };

            Assert.AreEqual(1, MapFieldClusters.Count(stacks));
        }

        [Test]
        public void DistantPairsAreSeparateFields()
        {
            var stacks = new[] { new Vector2(0f, 0f), new Vector2(8f, 6f), new Vector2(100f, 0f), new Vector2(106f, 8f) };

            Assert.AreEqual(2, MapFieldClusters.Count(stacks));
        }

        [Test]
        public void ChainOfStacksIsOneField()
        {
            var stacks = new[] { new Vector2(0f, 0f), new Vector2(36f, 0f), new Vector2(18f, 0f) };

            Assert.AreEqual(1, MapFieldClusters.Count(stacks));
        }
    }
}
