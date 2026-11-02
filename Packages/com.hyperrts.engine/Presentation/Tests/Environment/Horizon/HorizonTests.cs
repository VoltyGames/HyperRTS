using System.Collections.Generic;
using System.Linq;
using HyperRTS.Presentation.Environment.Horizon;
using NUnit.Framework;
using UnityEngine;

namespace HyperRTS.Presentation.Tests.Environment.Horizon
{
    /// <summary>The horizon's ring reaches from under the terrain out to its width, its belts lie on field edges and its tree line faces outward.</summary>
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
        public void Belts_LieOnFieldEdges()
        {
            var fields = Fields();
            var belts = new HorizonBelts { share = 0.35f }.Belts(fields, Seed, new Rect(-2000f, -2000f, 4000f, 4000f));

            Assert.IsNotEmpty(belts);
            foreach (var belt in belts)
            {
                var from = fields.ToGrid(belt.From);
                var to = fields.ToGrid(belt.To);
                var row = Mathf.RoundToInt(from.y / fields.size.y);
                Assert.AreEqual(row * fields.size.y, from.y, 0.01f, "a belt starts on a row's south edge");
                var shifted = from.x + fields.RowShift(Seed, row);
                Assert.AreEqual(0f, Mathf.Repeat(shifted + 0.005f, fields.size.x), 0.02f, "and at a field's west end");
                var run = (to - from).magnitude;
                var edge = Mathf.Abs(run - fields.size.x) < 0.01f || Mathf.Abs(run - fields.size.y) < 0.01f;
                Assert.IsTrue(edge, $"and runs one field edge, not {run} m");
            }
        }

        [Test]
        public void Belts_KeepTheirShareOfEdgesAndRepeatForASeed()
        {
            var fields = Fields();
            var area = new Rect(-3000f, -3000f, 6000f, 6000f);
            var belts = new HorizonBelts { share = 0.35f };

            var first = belts.Belts(fields, Seed, area);
            var again = belts.Belts(fields, Seed, area);
            var all = new HorizonBelts { share = 1f }.Belts(fields, Seed, area);

            CollectionAssert.AreEqual(first.Select(b => b.From), again.Select(b => b.From));
            Assert.AreEqual(0.35f, first.Count / (float)all.Count, 0.05f);
        }

        [Test]
        public void TreeLine_FacesOutwardAndDropsLoneColumns()
        {
            var line = new HorizonTreeLine(1);
            for (var i = 0; i < 4; i++)
            {
                line.Column(0, new Vector3(i * 9f, 0f, 0f), Vector2.up, 4f, 14f, 0.7f);
            }

            line.EndRun();
            line.Column(0, new Vector3(100f, 0f, 0f), Vector2.up, 4f, 14f, 0.7f);
            var mesh = line.Build();
            try
            {
                var (vertices, normals, triangles) = (mesh.vertices, mesh.normals, mesh.triangles);
                Assert.AreEqual(3 * 4 * 2 + 2 * 3, triangles.Length / 3, "three spans of four faces and two end caps");
                for (var t = 0; t < triangles.Length; t += 3)
                {
                    var (a, b, c) = (triangles[t], triangles[t + 1], triangles[t + 2]);
                    var face = Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]);
                    var middle = (vertices[a] + vertices[b] + vertices[c]) / 3f;
                    var centre = new Vector3(Mathf.Clamp(middle.x, 1f, 26f), 5f, 0f);
                    Assert.Greater(Vector3.Dot(face, middle - centre + (normals[a] + normals[b] + normals[c]) * 0.01f), 0f,
                        $"triangle {t / 3} faces outward");
                }
            }
            finally
            {
                Object.DestroyImmediate(mesh);
            }
        }
    }
}
