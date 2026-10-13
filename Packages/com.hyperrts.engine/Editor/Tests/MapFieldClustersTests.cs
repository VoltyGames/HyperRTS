using System.Linq;
using HyperRTS.Editor.Maps.Report;
using NUnit.Framework;
using UnityEngine;

namespace HyperRTS.Editor.Tests
{
    /// <summary>How the map report groups resource stacks into fields.</summary>
    public class MapFieldClustersTests
    {
        // Stacks are flattened x, z pairs.
        [TestCase(0, TestName = "NoStacksAreNoFields")]
        [TestCase(1, 0f, 0f, 8f, 6f, TestName = "ClosePairIsOneField")]
        [TestCase(2, 0f, 0f, 8f, 6f, 100f, 0f, 106f, 8f, TestName = "DistantPairsAreSeparateFields")]
        [TestCase(1, 0f, 0f, 36f, 0f, 18f, 0f, TestName = "ChainOfStacksIsOneField")]
        public void StacksGroupIntoFields(int expected, params float[] xz)
        {
            var stacks = Enumerable.Range(0, xz.Length / 2).Select(i => new Vector2(xz[i * 2], xz[i * 2 + 1])).ToList();

            Assert.AreEqual(expected, MapFieldClusters.Count(stacks));
        }
    }
}
