using System.Collections.Generic;
using System.Linq;
using HyperRTS.Editor.Maps.Common;
using HyperRTS.Editor.Maps.Geometry;
using HyperRTS.Editor.Maps.Land;
using HyperRTS.Editor.Maps.Meshes;
using HyperRTS.Editor.Maps.Planning;
using HyperRTS.Editor.Maps.Spec;
using NUnit.Framework;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Tests
{
    /// <summary>Ponds: the basin below the water, the dam above it, the water sheet, copies and what a pond may not touch.</summary>
    public class MapPondTests
    {
        private const float Water = 12f;
        private const float Ground = 13f;

        // The dam is the bottom edge (z = -60); water spans x -20 to 20, z -60 to -30.
        private static MapPondSpec Pond() => new()
        {
            points = new List<MapPoint> { new(-20f, -60f), new(20f, -60f), new(20f, -30f), new(-20f, -30f) },
            shore = "Verge", dam = 0, damSurface = "Dirt",
        };

        private static MapSpec Spec(MapPondSpec pond)
        {
            var spec = MapFixture.Spec();
            spec.terrain.height = Ground;
            spec.terrain.waterLevel = Water;
            spec.ponds.Add(pond);
            return spec;
        }

        private static MapPlanContext Context(MapSpec spec, MapIssues issues) =>
            MapFixture.Context(spec, MapFixture.Climate(), issues);

        [Test]
        public void Basin_SitsBelowTheWaterAndTheDamAboveIt()
        {
            var heights = Context(Spec(Pond()), new MapIssues()).Heights;

            Assert.Less(heights.Height(new Vector2(0f, -45f)), Water - 1.5f, "pond centre");
            Assert.Greater(heights.Height(new Vector2(0f, -66f)), Ground + 0.4f, "dam top");
            Assert.AreEqual(Ground, heights.Height(Vector2.zero), 0.3f, "open ground");
        }

        [Test]
        public void Planner_ClaimsWaterThenTheDamAndTheWetMargin()
        {
            var context = Context(Spec(Pond()), new MapIssues());

            new MapWaterPlanner(context).Plan();
            new MapPondPlanner(context).Plan();

            Assert.AreEqual(MapLandKind.Water, context.Land.KindAt(new Vector2(0f, -45f)));
            Assert.AreEqual("Dirt", context.Land.At(new Vector2(0f, -66f)).Surface, "dam top");
            Assert.AreEqual("Verge", context.Land.At(new Vector2(0f, -27f)).Surface, "wet margin");
        }

        [Test]
        public void WaterSheet_StaysWithinAMarginOfTheShoreAndCoversThePond()
        {
            var context = Context(Spec(Pond()), new MapIssues());
            var mesh = MapWaterMesh.Build(context.Shapes, MapFixture.Climate(), Water, context.Layout.TerrainBounds);
            var ponds = context.Shapes.Ponds;

            var used = mesh.triangles.Distinct().Select(i => new Vector2(mesh.vertices[i].x, mesh.vertices[i].z)).ToList();

            Assert.IsNotEmpty(used);
            // The map is symmetric, so the sheet covers the copy too; EdgeDistance is infinite past the bank.
            float ShoreDistance(Vector2 p) => ponds.Min(pond => -MapGeometry.SignedDistance(pond.Shore, p));
            Assert.That(used.All(p => ShoreDistance(p) < 6f), "a vertex lies far from the ponds");
            Assert.That(used.Any(p => ShoreDistance(p) < -5f), "the middle is covered");
            Assert.That(mesh.colors.All(c => c.b > 0.99f), "still water is marked in vertex blue");
            Object.DestroyImmediate(mesh);
        }

        [Test]
        public void Copy_IsTurnedAroundTheCentreWithItsDam()
        {
            var context = Context(Spec(Pond()), new MapIssues());
            var ponds = context.Shapes.Ponds;

            Assert.AreEqual(2, ponds.Count);
            Assert.Less(ponds[1].EdgeDistance(new Vector2(0f, 45f)), 0f, "copy's water");
            Assert.That(ponds[1].OnDam(new Vector2(0f, 66f)), "copy's dam");
            Assert.AreEqual(context.Heights.Height(new Vector2(3f, -66f)), context.Heights.Height(new Vector2(-3f, 66f)), 0.01f);
        }

        [TestCase(-5f, -95f, "base")]
        [TestCase(-5f, -45f, "road")]
        public void PondOnABaseOrARoad_FailsNamingIt(float x, float z, string what)
        {
            var pond = Pond();
            pond.points = new List<MapPoint> { new(x - 8f, z - 8f), new(x + 8f, z - 8f), new(x + 8f, z + 8f), new(x - 8f, z + 8f) };
            pond.dam = -1;
            var spec = Spec(pond);
            spec.roads.Add(MapFixture.Road("Dirt", new Vector2(-60f, -45f), new Vector2(60f, -45f)));
            var issues = new MapIssues();
            var context = MapFixture.Context(spec, MapFixture.Climate(), issues);

            new MapPondPlanner(context).Plan();

            Assert.That(issues.Errors.Any(e => e.Message.Contains("Pond 1 crosses") && e.Message.Contains(what)), issues.ToString());
        }
    }
}
