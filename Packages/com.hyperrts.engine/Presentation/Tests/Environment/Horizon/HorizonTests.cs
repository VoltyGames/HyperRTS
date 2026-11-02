using System.Collections.Generic;
using System.Linq;
using HyperRTS.Presentation.Environment.Horizon;
using NUnit.Framework;
using UnityEngine;

namespace HyperRTS.Presentation.Tests.Environment.Horizon
{
    /// <summary>The horizon's ring reaches from under the terrain out to its width, and its fields' texture covers it.</summary>
    public class HorizonTests
    {
        private const int Seed = 30871;
        private static readonly Rect Terrain = new(-420f, -420f, 840f, 840f);

        private static HorizonFields Fields() => new()
        {
            size = new Vector2(272f, 144f),
            angle = 12f,
            crops = new List<HorizonCrop> { new() { layer = 3, weight = 1f } },
        };

        [Test]
        public void Offsets_StartUnderTheTerrainAndEndAtTheWidth()
        {
            var offsets = HorizonRing.Offsets(2500f);

            Assert.AreEqual(-HorizonRing.Tuck, offsets[0]);
            Assert.AreEqual(0f, offsets[1]);
            Assert.AreEqual(2500f, offsets[^1]);
            for (var i = 2; i < offsets.Count - 1; i++)
            {
                Assert.Greater(offsets[i] - offsets[i - 1], offsets[i - 1] - offsets[i - 2] - 0.01f, "steps grow outward");
            }
        }

        [Test]
        public void Ring_ReachesUnderTheTerrainAndOutToItsWidth()
        {
            var mesh = HorizonRing.Build(Terrain, 2500f, _ => 0f, _ => 0f);
            try
            {
                var points = mesh.vertices.Select(v => new Vector2(v.x, v.z)).ToList();
                var distances = points.Select(p => HorizonRing.Distance(Terrain, p)).ToList();

                Assert.IsTrue(points.Any(p => Terrain.Contains(p)), "the first loop tucks under the terrain");
                Assert.AreEqual(2500f, distances.Max(), 1f);
                Assert.IsTrue(mesh.normals.All(n => n.y > 0.99f), "flat ground faces up");
            }
            finally
            {
                Object.DestroyImmediate(mesh);
            }
        }

        [Test]
        public void Cells_HoldEveryPointOfTheArea()
        {
            var fields = Fields();
            var area = new Rect(-2900f, -2900f, 5800f, 5800f);
            var cells = fields.Cells(area);

            for (var x = area.xMin; x <= area.xMax; x += 25f)
            {
                for (var y = area.yMin; y <= area.yMax; y += 25f)
                {
                    var grid = fields.ToGrid(new Vector2(x, y));
                    var row = Mathf.FloorToInt(grid.y / fields.size.y);
                    var column = Mathf.FloorToInt((grid.x + fields.RowShift(Seed, row)) / fields.size.x);
                    Assert.IsTrue(cells.Contains(new Vector2Int(column, row)), $"({x}, {y}) is in field ({column}, {row})");
                }
            }
        }

        [Test]
        public void Texture_ListsTheCropsAndSlidesEachRow()
        {
            var fields = Fields();
            fields.crops.Add(new HorizonCrop { layer = 5, weight = 3f });
            var material = new Material(Shader.Find("Hidden/InternalErrorShader"));
            var cells = fields.Cells(new Rect(-1000f, -1000f, 2000f, 2000f));
            var texture = fields.Apply(material, Seed, cells);
            try
            {
                Assert.AreEqual(new Color(3f, 0.25f, 0f, 0f), texture.GetPixel(0, 0));
                Assert.AreEqual(new Color(5f, 0.75f, 0f, 0f), texture.GetPixel(1, 0));
                var layers = new HashSet<float>();
                for (var row = 0; row < cells.height; row++)
                {
                    for (var column = 0; column < cells.width; column++)
                    {
                        var field = texture.GetPixel(column, row + 1);
                        Assert.AreEqual(fields.RowShift(Seed, cells.yMin + row), field.b, "each field carries its row's slide");
                        layers.Add(field.r);
                    }
                }

                CollectionAssert.AreEquivalent(new[] { 3f, 5f }, layers, "the fields grow both crops");
            }
            finally
            {
                Object.DestroyImmediate(texture);
                Object.DestroyImmediate(material);
            }
        }
    }
}
