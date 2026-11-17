using System.Collections.Generic;
using HyperRTS.Presentation.Environment.Ground.Holes;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace HyperRTS.Presentation.Tests.Environment.Ground.Holes
{
    /// <summary>Which hole texels a building's rectangle covers, and holes cut into and put back on a real terrain.</summary>
    public class TerrainHolesTests
    {
        private static readonly HoleGrid Grid = new(new float2(-32f), new float2(64f), 64);
        private static readonly float2 East = new(1f, 0f);

        [Test]
        public void SquareOnTheTexelGrid_CoversEveryTexelInsideIt()
        {
            var holes = Holes(new HoleFootprint(new float2(10f, 10f), East, new float2(4f, 4f)));

            Assert.AreEqual(16, Count(holes, false));
            Assert.IsFalse(holes[10 + 32 - 1, 10 + 32 - 1]);
            Assert.IsTrue(holes[10 + 32 + 2, 10 + 32]);
        }

        [Test]
        public void TurnedSquare_CoversTheDiamondAround()
        {
            var turned = math.normalize(new float2(1f, 1f));

            var holes = Holes(new HoleFootprint(new float2(10f, 10f), turned, new float2(4f, 4f)));

            Assert.AreEqual(12, Count(holes, false));
            Assert.IsTrue(holes[10 + 32 + 1, 10 + 32 + 1], "a corner of the unturned square stayed in");
        }

        [Test]
        public void RectangleOverTheEdge_IsClampedToTheTerrain()
        {
            var footprint = new HoleFootprint(new float2(-32f, 0f), East, new float2(4f, 4f));

            var bounds = footprint.Bounds(Grid);

            Assert.AreEqual(new RectInt(0, 30, 2, 4), bounds);
            Assert.AreEqual(8, Count(Holes(footprint), false));
        }

        [Test]
        public void RectangleOffTheTerrain_HasNoTexels()
        {
            var footprint = new HoleFootprint(new float2(100f, 0f), East, new float2(4f, 4f));

            Assert.AreEqual(0, footprint.Bounds(Grid).width);
        }

        [Test]
        public void Terrain_GetsTheHoleAndTheOverlapSurvivesOneBuildingGoing()
        {
            var data = new TerrainData { heightmapResolution = 65, size = new Vector3(64f, 20f, 64f) };
            var terrain = Terrain.CreateTerrainGameObject(data).GetComponent<Terrain>();
            terrain.transform.position = new Vector3(-32f, 0f, -32f);
            var holes = new TerrainHoles(terrain);
            var first = new Entity { Index = 1, Version = 1 };
            var second = new Entity { Index = 2, Version = 1 };
            try
            {
                var both = new Dictionary<Entity, HoleFootprint>
                {
                    [first] = new(new float2(10f, 10f), East, new float2(4f, 4f)),
                    [second] = new(new float2(12f, 10f), East, new float2(4f, 4f)),
                };
                holes.Apply(both);
                Assert.AreEqual(24, Count(data.GetHoles(0, 0, 64, 64), false));

                both.Remove(first);
                holes.Apply(both);
                Assert.AreEqual(16, Count(data.GetHoles(0, 0, 64, 64), false), "the second building's cut was filled");
            }
            finally
            {
                holes.Dispose();
            }

            Assert.AreEqual(0, Count(data.GetHoles(0, 0, 64, 64), false), "the asset kept a hole");
            Object.DestroyImmediate(terrain.gameObject);
            Object.DestroyImmediate(data);
        }

        private static bool[,] Holes(HoleFootprint footprint)
        {
            var holes = new bool[64, 64];
            for (var y = 0; y < 64; y++)
            {
                for (var x = 0; x < 64; x++)
                {
                    holes[y, x] = true;
                }
            }

            footprint.Cut(Grid, new RectInt(0, 0, 64, 64), holes);
            return holes;
        }

        private static int Count(bool[,] holes, bool value)
        {
            var count = 0;
            foreach (var hole in holes)
            {
                count += hole == value ? 1 : 0;
            }

            return count;
        }
    }
}
