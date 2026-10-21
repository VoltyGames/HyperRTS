using HyperRTS.Presentation.Environment;
using NUnit.Framework;
using UnityEngine;

namespace HyperRTS.Presentation.Tests
{
    /// <summary>Scatter tile culling maths: a tile's box and the camera's distances to it.</summary>
    public class ScatterTilesTests
    {
        [Test]
        public void TileBounds_WidenTheSquareByTheOverhang_UpToTheTallestPlant()
        {
            var bounds = ScatterTiles.TileBounds(new Rect(0f, 0f, 16f, 16f), 1f, 3f, 2f);
            Assert.AreEqual(new Vector3(-ScatterTiles.Overhang, 1f, -ScatterTiles.Overhang), bounds.min);
            Assert.AreEqual(new Vector3(16f + ScatterTiles.Overhang, 5f, 16f + ScatterTiles.Overhang), bounds.max);
        }

        [Test]
        public void Distances_AreToTheNearestAndFarthestPointOfTheBox()
        {
            var bounds = ScatterTiles.TileBounds(new Rect(100f, 0f, 16f, 16f), 0f, 0f, 2f);
            var (near, far) = ScatterTiles.Distances(bounds, new Vector3(0f, 1f, 8f));
            Assert.AreEqual(100f - ScatterTiles.Overhang, near, 1e-4f);
            Assert.Greater(far, 116f);
        }
    }
}
