using HyperRTS.Editor.Maps.Build;
using HyperRTS.Editor.Maps.Data;
using HyperRTS.Editor.Maps.Geometry;
using HyperRTS.Editor.Maps.Ground;
using NUnit.Framework;
using UnityEngine;

namespace HyperRTS.Editor.Tests
{
    /// <summary>Road ribbons: UV and tangent layout, alpha fades, terrain conforming, breaks, and which ends fade.</summary>
    public class MapRoadMeshTests
    {
        private const int Columns = 5;

        private static readonly MapRoadClass Asphalt = new() { key = "Asphalt", width = 6f, rank = 2 };
        private static readonly MapRoadClass Dirt = new() { key = "Dirt", width = 4f, rank = 0 };
        private static readonly MapRoadClass Gravel = new() { key = "Gravel", width = 4f, rank = 5 };
        private static readonly MapRoadClass Lane = new() { key = "Lane", width = 4f, rank = 1 };

        private static MapHeights Flat() => new(129, new Rect(-64f, -64f, 128f, 128f));

        private static MapRoadMesh Ribbon(MapRoad road, bool fadeStart, bool fadeEnd, MapHeights heights, float datum = 0f,
            System.Func<Vector2, bool> hidden = null)
        {
            var mesh = new MapRoadMesh();
            MapRoadRibbon.Add(mesh, road, fadeStart, fadeEnd, heights, datum, hidden ?? (_ => false));
            return mesh;
        }

        private static MapRoad North() =>
            MapRoadFixture.RoadShape(Asphalt, false, new Vector2(0f, -30f), new Vector2(0f, 30f));

        [Test]
        public void Uvs_RunZeroToOneAcross_AndMetresAlong()
        {
            var mesh = Ribbon(North(), true, false, Flat());

            Assert.AreEqual(31 * Columns, mesh.Count, "a row every 2 m along 60 m");
            for (var c = 0; c < Columns; c++)
            {
                Assert.AreEqual(c / (Columns - 1f), mesh.Uvs[c].x, 1e-4f);
                Assert.AreEqual(0f, mesh.Uvs[c].y, 1e-4f);
                Assert.AreEqual(-3f + 1.5f * c, mesh.Vertices[c].x, 1e-3f, "left to right, ±width/2");
                Assert.AreEqual(2f * 5f, mesh.Uvs[5 * Columns + c].y, 1e-3f);
            }

            Assert.AreEqual(60f, mesh.Uvs[^1].y, 1e-3f);
        }

        [Test]
        public void Tangents_PointAcrossTheRoad_AndNormalsUp()
        {
            var mesh = Ribbon(North(), false, false, Flat());

            var tangent = mesh.Tangents[0];
            Assert.AreEqual(1f, tangent.x, 1e-4f, "U grows to the right of a road heading north");
            Assert.AreEqual(0f, tangent.z, 1e-4f);
            Assert.AreEqual(-1f, tangent.w, "bitangent along the road, as Unity's own planes");
            Assert.AreEqual(1f, mesh.Normals[0].y, 1e-4f);
        }

        [Test]
        public void Alpha_FadesOverFourMetresAtTheFadingEndOnly()
        {
            var mesh = Ribbon(North(), true, false, Flat());

            Assert.AreEqual(0, mesh.Colors[0].a, "starts clear");
            Assert.That(mesh.Colors[Columns].a, Is.InRange(126, 129), "half way at 2 m");
            Assert.AreEqual(255, mesh.Colors[2 * Columns].a, "solid from 4 m");
            Assert.AreEqual(255, mesh.Colors[^1].a, "the other end is solid");
        }

        [Test]
        public void Triangles_FaceUp()
        {
            var mesh = Ribbon(North(), false, false, Flat());

            Assert.AreEqual(30 * (Columns - 1) * 6, mesh.Triangles.Count);
            for (var i = 0; i < mesh.Triangles.Count; i += 3)
            {
                var a = mesh.Vertices[mesh.Triangles[i]];
                var b = mesh.Vertices[mesh.Triangles[i + 1]];
                var c = mesh.Vertices[mesh.Triangles[i + 2]];
                Assert.Greater(Vector3.Cross(b - a, c - a).y, 0f, $"triangle {i / 3} is clockwise from above");
            }
        }

        [Test]
        public void Vertices_FollowTheTerrain_AFewCentimetresOver()
        {
            var heights = Flat();
            for (var x = 0; x < heights.Resolution; x++)
            {
                for (var z = 0; z < heights.Resolution; z++)
                {
                    heights[x, z] = 10f + heights.Position(x, z).x * 0.2f;
                }
            }

            var mesh = Ribbon(North(), false, false, heights, 10f);

            Assert.AreEqual(-0.6f + MapRoadRibbon.Lift, mesh.Vertices[0].y, 0.01f, "3 m west on a 0.2 slope, datum removed");
            Assert.AreEqual(0.6f + MapRoadRibbon.Lift, mesh.Vertices[Columns - 1].y, 0.01f);
            Assert.Less(mesh.Normals[0].x, -0.1f, "the normal leans away from the rising side");
        }

        [Test]
        public void HiddenRows_BreakTheRibbon()
        {
            var mesh = Ribbon(North(), false, false, Flat(), 0f, p => Mathf.Abs(p.y) < 5f);

            Assert.AreEqual(26 * Columns, mesh.Count, "five rows from -4 to 4 m are dropped");
            Assert.AreEqual(24 * (Columns - 1) * 6, mesh.Triangles.Count, "no quads span the gap");
        }

        [Test]
        public void FreeEnds_AndEndsUnderAHigherOrEqualRoad_Fade()
        {
            var main = MapRoadFixture.RoadShape(Asphalt, false, new Vector2(-50f, 0f), new Vector2(50f, 0f));
            var side = MapRoadFixture.RoadShape(Dirt, false, new Vector2(0f, -40f), new Vector2(0f, 0f));
            var roads = new[] { main, side };

            Assert.AreEqual((true, true), MapRoadFades.For(side, roads), "the dirt fades out under the asphalt");
            Assert.AreEqual((true, true), MapRoadFades.For(main, roads), "free ends");
        }

        [Test]
        public void ARoadRunningOntoALowerRank_StaysSolidThere()
        {
            var main = MapRoadFixture.RoadShape(Dirt, false, new Vector2(-50f, 0f), new Vector2(50f, 0f));
            var top = MapRoadFixture.RoadShape(Gravel, false, new Vector2(0f, -40f), new Vector2(0f, 0f));

            Assert.AreEqual((true, false), MapRoadFades.For(top, new[] { main, top }));
        }

        [Test]
        public void RoadsThatCarryOnIntoEachOther_DoNotFadeWhereTheyMeet()
        {
            var first = MapRoadFixture.RoadShape(Lane, false, new Vector2(0f, -50f), new Vector2(0f, 0f));
            var second = MapRoadFixture.RoadShape(Lane, false, new Vector2(0f, 0f), new Vector2(0f, 50f));
            var roads = new[] { first, second };

            Assert.AreEqual((true, false), MapRoadFades.For(first, roads));
            Assert.AreEqual((false, true), MapRoadFades.For(second, roads));
        }
    }
}
