using System.Collections.Generic;
using HyperRTS.Editor.Maps;
using HyperRTS.Editor.Maps.Data;
using HyperRTS.Editor.Maps.Geometry;
using HyperRTS.Editor.Maps.Layout;
using HyperRTS.Editor.Maps.Meshes;
using HyperRTS.Editor.Maps.Spec;
using HyperRTS.Editor.Maps.Symmetry;
using NUnit.Framework;
using UnityEngine;

namespace HyperRTS.Editor.Tests
{
    /// <summary>River ribbons: vertex colour fades the water out over fords and marks their fast water.</summary>
    public class MapWaterMeshTests
    {
        private const float RiverZ = -60f;

        [Test]
        public void Ribbon_FadesTheWaterAndMarksFastWaterOverAFord()
        {
            var (mesh, shapes) = Build();
            var colors = mesh.colors;
            var nearest = NearestRow(mesh.vertices, shapes.Crossings[0].Position);

            Assert.Less(colors[nearest].a, 1f, "the water fades out over the ford");
            Assert.Greater(colors[nearest].g, 0f, "the ford runs fast");
            Assert.AreEqual(colors[nearest], colors[nearest + 1], "both banks of a row match");
        }

        [Test]
        public void Ribbon_KeepsFullCalmWaterAwayFromFords()
        {
            var (mesh, shapes) = Build();
            var (vertices, colors) = (mesh.vertices, mesh.colors);
            var far = 0;
            for (var i = 0; i < vertices.Length; i += 2)
            {
                var centre = RowCentre(vertices, i);
                if (shapes.Crossings.Exists(c => (c.Position - centre).magnitude < 40f))
                {
                    continue;
                }

                Assert.AreEqual(1f, colors[i].a, $"full water at {centre}");
                Assert.AreEqual(0f, colors[i].g, $"calm water at {centre}");
                far++;
            }

            Assert.Greater(far, 20);
        }

        private static (Mesh, MapShapes) Build()
        {
            var spec = new MapSpec
            {
                name = "Test", climate = "Test", mode = "1v1", seed = 5,
                size = new MapPoint(240f, 240f), symmetry = MapSymmetry.Rotate180,
                terrain = new MapTerrainSpec { resolution = 257, margin = 20f, relief = 0f },
                spawns = new List<MapSpawnSpec> { new() { pos = new MapPoint(0f, -100f) } },
            };
            spec.rivers.Add(new MapRiverSpec
            {
                points = new List<MapPoint> { new(-110f, RiverZ), new(110f, RiverZ) }, width = 10f, valleyWidth = 30f,
                crossings = new List<MapCrossingSpec> { new() { t = 0.5f, kind = MapCrossingSpec.Ford } },
            });
            var layout = MapExpander.Expand(spec, new MapIssues());
            var shapes = new MapShapes(layout, new MapPalette(), new MapClimate());
            Assert.IsNotEmpty(shapes.Crossings);
            return (MapWaterMesh.Build(shapes, layout.WaterHeight, layout.TerrainBounds), shapes);
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
