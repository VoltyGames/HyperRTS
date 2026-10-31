using System.Collections.Generic;
using HyperRTS.Editor.Maps;
using HyperRTS.Editor.Maps.Data;
using HyperRTS.Editor.Maps.Geometry;
using HyperRTS.Editor.Maps.Ground;
using HyperRTS.Editor.Maps.Layout;
using HyperRTS.Editor.Maps.Meshes;
using HyperRTS.Editor.Maps.Spec;
using NUnit.Framework;
using Unity.Scripting.LifecycleManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace HyperRTS.Editor.Tests.Maps.Roads
{
    /// <summary>Road and river ribbons: layout, fades, terrain conforming, breaks, draw order and water over fords.</summary>
    public class MapRibbonMeshTests
    {
        private const int Columns = 5;
        private const float RiverZ = -60f;

        private MapRoadClass Asphalt;
        private MapRoadClass Dirt;
        private MapRoadClass Gravel;
        private MapRoadClass Lane;

        [SetUp]
        public void SetUp()
        {
            Asphalt = RoadClass("Asphalt", 6f, 2);
            Dirt = RoadClass("Dirt", 4f, 0);
            Gravel = RoadClass("Gravel", 4f, 5);
            Lane = RoadClass("Lane", 4f, 1);
        }

        private static MapRoadClass RoadClass(string key, float width, int rank) =>
            MapFixture.Item<MapRoadClass>(key, i =>
            {
                i.width = width;
                i.rank = rank;
            });

        private static MapHeights Flat() => new(129, new Rect(-64f, -64f, 128f, 128f));

        private static MapRoadMesh Ribbon(MapRoad road, bool fadeStart, bool fadeEnd, MapHeights heights, float groundLevel = 0f,
            System.Func<Vector2, bool> hidden = null)
        {
            var mesh = new MapRoadMesh();
            mesh.AddRibbon(road, fadeStart, fadeEnd, heights, groundLevel, hidden ?? (_ => false));
            return mesh;
        }

        private MapRoad North() =>
            MapFixture.RoadShape(Asphalt, false, new Vector2(0f, -30f), new Vector2(0f, 30f));

        [Test]
        public void RoadShader_DrawsInTheOpaquePassAfterTerrain()
        {
            // URP sorts opaques by sorting order before queue, so the class rank on each renderer stacks overlapping roads.
            var road = Shader.Find("HyperRTS/Road").renderQueue;
            var terrain = Shader.Find("HyperRTS/Terrain").renderQueue;

            Assert.That(road, Is.GreaterThan(terrain), "roads blend over the finished terrain");
            Assert.That(road, Is.LessThanOrEqualTo((int)RenderQueue.GeometryLast), "sorting order only leads in the opaque pass");
        }

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

            Assert.AreEqual(-0.6f + MapRoadMesh.Lift, mesh.Vertices[0].y, 0.01f, "3 m west on a 0.2 slope, groundLevel removed");
            Assert.AreEqual(0.6f + MapRoadMesh.Lift, mesh.Vertices[Columns - 1].y, 0.01f);
            Assert.Less(mesh.Normals[0].x, -0.1f, "the normal leans away from the rising side");
        }

        [Test]
        public void HiddenRows_BreakTheRibbon_AndFadeItOutAtTheBreak_EvenOnASolidRoad()
        {
            var mesh = Ribbon(North(), false, false, Flat(), 0f, p => Mathf.Abs(p.y) < 5f);

            Assert.AreEqual(26 * Columns, mesh.Count, "five rows from -4 to 4 m are dropped");
            Assert.AreEqual(24 * (Columns - 1) * 6, mesh.Triangles.Count, "no quads span the gap");
            Assert.AreEqual(255, mesh.Colors[0].a, "the road's own end stays solid");
            Assert.AreEqual(0, mesh.Colors[12 * Columns].a, "clear at -6 m, the last row before the water");
            Assert.That(mesh.Colors[11 * Columns].a, Is.InRange(126, 129), "half way 2 m back");
            Assert.AreEqual(0, mesh.Colors[13 * Columns].a, "clear at 6 m, the first row after it");
            Assert.AreEqual(255, mesh.Colors[^1].a);
        }

        [Test]
        public void FreeEnds_AndEndsUnderAHigherOrEqualRoad_Fade()
        {
            var main = MapFixture.RoadShape(Asphalt, false, new Vector2(-50f, 0f), new Vector2(50f, 0f));
            var side = MapFixture.RoadShape(Dirt, false, new Vector2(0f, -40f), new Vector2(0f, 0f));
            var roads = new[] { main, side };

            Assert.AreEqual((true, true), MapRoadFades.For(side, roads), "the dirt fades out under the asphalt");
            Assert.AreEqual((true, true), MapRoadFades.For(main, roads), "free ends");
        }

        [Test]
        public void ARoadRunningOntoALowerRank_StaysSolidThere()
        {
            var main = MapFixture.RoadShape(Dirt, false, new Vector2(-50f, 0f), new Vector2(50f, 0f));
            var top = MapFixture.RoadShape(Gravel, false, new Vector2(0f, -40f), new Vector2(0f, 0f));

            Assert.AreEqual((true, false), MapRoadFades.For(top, new[] { main, top }));
        }

        [Test]
        public void RoadsThatCarryOnIntoEachOther_DoNotFadeWhereTheyMeet()
        {
            var first = MapFixture.RoadShape(Lane, false, new Vector2(0f, -50f), new Vector2(0f, 0f));
            var second = MapFixture.RoadShape(Lane, false, new Vector2(0f, 0f), new Vector2(0f, 50f));
            var roads = new[] { first, second };

            Assert.AreEqual((true, false), MapRoadFades.For(first, roads));
            Assert.AreEqual((false, true), MapRoadFades.For(second, roads));
        }

        [Test]
        public void River_MarksShallowAndFastWaterOverAFord()
        {
            var (mesh, shapes) = River();
            var colors = mesh.colors;
            var nearest = NearestRow(mesh.vertices, shapes.Crossings[0].Position);

            Assert.Greater(colors[nearest].a, 0f, "the bed is shallow over the ford");
            Assert.Greater(colors[nearest].g, 0f, "the ford runs fast");
            Assert.AreEqual(colors[nearest], colors[nearest + 1], "both banks of a row match");
        }

        [Test]
        public void River_KeepsDeepCalmWaterAwayFromFords()
        {
            var (mesh, shapes) = River();
            var (vertices, colors) = (mesh.vertices, mesh.colors);
            var far = 0;
            for (var i = 0; i < vertices.Length; i += 2)
            {
                var centre = RowCentre(vertices, i);
                if (shapes.Crossings.Exists(c => (c.Position - centre).magnitude < 40f))
                {
                    continue;
                }

                Assert.AreEqual(0f, colors[i].a, $"deep water at {centre}");
                Assert.AreEqual(0f, colors[i].g, $"calm water at {centre}");
                far++;
            }

            Assert.Greater(far, 20);
        }

        private static (Mesh, MapShapes) River()
        {
            var spec = MapFixture.Spec();
            spec.seed = 5;
            spec.rivers.Add(new MapRiverSpec
            {
                points = new List<MapPoint> { new(-110f, RiverZ), new(110f, RiverZ) }, width = 10f, valleyWidth = 30f,
                crossings = new List<MapCrossingSpec> { new() { along = 0.5f, kind = MapCrossingSpec.Ford } },
            });
            var layout = MapExpander.Expand(spec, new MapIssues());
            var climate = MapFixture.Item<MapClimate>("Climate", i => { i.fordShallowsWidth = 6f; i.fordRapidsLength = 10f; });
            var shapes = new MapShapes(layout, MapFixture.Item<MapPalette>("Palette"), climate);
            Assert.IsNotEmpty(shapes.Crossings);
            return (MapWaterMesh.Build(shapes, climate, layout.WaterHeight, layout.TerrainBounds), shapes);
        }

        private static int NearestRow(Vector3[] vertices, Vector2 point)
        {
            var best = 0;
            for (var i = 0; i < vertices.Length; i += 2)
            {
                if ((RowCentre(vertices, i) - point).sqrMagnitude < (RowCentre(vertices, best) - point).sqrMagnitude)
                {
                    best = i;
                }
            }

            return best;
        }

        private static Vector2 RowCentre(Vector3[] vertices, int row)
        {
            var centre = (vertices[row] + vertices[row + 1]) * 0.5f;
            return new Vector2(centre.x, centre.z);
        }
    }
}
