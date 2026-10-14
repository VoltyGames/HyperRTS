using System.Linq;
using HyperRTS.Editor.Maps;
using HyperRTS.Editor.Maps.Checks;
using HyperRTS.Editor.Maps.Data;
using HyperRTS.Editor.Maps.Geometry;
using HyperRTS.Editor.Maps.Land;
using HyperRTS.Editor.Maps.Planning;
using HyperRTS.Editor.Maps.Symmetry;
using NUnit.Framework;
using UnityEngine;

namespace HyperRTS.Editor.Tests
{
    /// <summary>Ground decals laid on surfaces at build time: how many, where their copies go, and their keys.</summary>
    public class MapDecalTests
    {
        [Test]
        public void SurfaceDecals_LayTheirDensityPerHectare_InEveryCopy()
        {
            var climate = Climate(50f);
            var context = MapRoadFixture.Context(MapRoadFixture.Spec(), climate, new MapIssues());
            var area = new Rect(-60f, -80f, 80f, 60f);
            context.Land.ClaimWhere(area, context.Region(MapLandKind.Margin, "Muddy"), _ => true);

            new MapDecalPlanner(context).Plan();

            var expected = Mathf.RoundToInt(50f * area.width * area.height / 10000f);
            var near = new Rect(area.x - MapLand.CellSize, area.y - MapLand.CellSize, area.width + MapLand.CellSize * 2f,
                area.height + MapLand.CellSize * 2f);
            var laid = context.Decals.Count(d => near.Contains(d.Position));
            // The land grid rounds the claimed area to whole cells.
            Assert.That(laid, Is.InRange(expected - 1, expected + 1));
            Assert.AreEqual(laid * 2, context.Decals.Count);
        }

        [Test]
        public void MirroredMaps_LayMirroredDecals()
        {
            var spec = MapRoadFixture.Spec();
            spec.symmetry = MapSymmetry.MirrorXName;
            spec.spawns[0].pos = new Maps.Spec.MapPoint(-100f, 0f);
            var context = MapRoadFixture.Context(spec, Climate(80f), new MapIssues());
            var area = new Rect(-90f, -40f, 60f, 80f);
            context.Land.ClaimWhere(area, context.Region(MapLandKind.Margin, "Muddy"), _ => true);

            new MapDecalPlanner(context).Plan();

            var authored = context.Decals.Where(d => area.Contains(d.Position)).ToList();
            Assert.Greater(authored.Count, 10);
            foreach (var decal in authored)
            {
                var mirror = MapTransform.MirrorX;
                var twin = context.Decals.Single(d => (d.Position - mirror.Apply(decal.Position)).sqrMagnitude < 1e-6f);
                Assert.AreEqual(decal.Diameter, twin.Diameter);
                Assert.AreEqual(mirror.ApplyYaw(decal.Yaw), twin.Yaw, 1e-3f);
                Assert.AreNotEqual(decal.Mirrored, twin.Mirrored);
            }
        }

        [Test]
        public void AnUnknownDecalKey_IsAnErrorNamingIt()
        {
            var climate = Climate(10f);
            climate.surfaces.Single(s => s.key == "Muddy").decals.Add(new MapDecalDensity { decal = "Tar", perHectare = 1f });
            var palette = new MapPalette();
            palette.entries.Add(new MapPaletteEntry { key = "Shed", groundDecal = "Oil" });
            var issues = new MapIssues();

            MapAssetCheck.Check(climate, palette, issues);

            Assert.IsTrue(issues.Errors.Any(e => e.Contains("Muddy") && e.Contains("Tar")), issues.ToString());
            Assert.IsTrue(issues.Errors.Any(e => e.Contains("Shed") && e.Contains("Oil")), issues.ToString());
        }

        [Test]
        public void AGroundDecalWithoutASize_IsAnErrorNamingIt()
        {
            var palette = new MapPalette();
            palette.entries.Add(new MapPaletteEntry { key = "Shed", groundDecal = "Puddle" });
            var issues = new MapIssues();

            MapAssetCheck.Check(Climate(10f), palette, issues);

            Assert.IsTrue(issues.Errors.Any(e => e.Contains("Shed") && e.Contains("groundDecalSize")), issues.ToString());
        }

        [Test]
        public void AlongRoadDecals_SitOnTheTrackCentre_TurnedAlongIt_InEveryCopy()
        {
            var climate = Climate(0f);
            climate.decals.Add(new MapClimateDecal { key = "Ruts", size = new Vector2(3f, 5f), randomRotation = false, alongRoad = true });
            var rutted = MapClimateFixture.Surface("Rutted", "Dirt");
            rutted.decals.Add(new MapDecalDensity { decal = "Ruts", perHectare = 400f });
            climate.surfaces.Add(rutted);
            var context = MapRoadFixture.Context(MapRoadFixture.Spec(), climate, new MapIssues());
            var dirt = climate.roadClasses.Single(c => c.key == "Dirt");
            var track = MapRoadFixture.RoadShape(dirt, true, new Vector2(-90f, -60f), new Vector2(30f, -20f));
            context.Shapes.AddRoad(track);
            context.Shapes.AddRoad(MapRoadFixture.RoadShape(dirt, true, new Vector2(90f, 60f), new Vector2(-30f, 20f)));
            var area = new Rect(-100f, -70f, 140f, 60f);
            context.Land.ClaimWhere(area, context.Region(MapLandKind.Margin, "Rutted"), p => track.Line.Distance(p) < 2f);

            new MapDecalPlanner(context).Plan();

            var heading = MapGeometry.Yaw(new Vector2(120f, 40f));
            var authored = context.Decals.Where(d => d.Kind.key == "Ruts" && d.Position.y < 0f).ToList();
            Assert.Greater(authored.Count, 3);
            foreach (var decal in authored)
            {
                Assert.AreEqual(0f, track.Line.Distance(decal.Position), 0.05f, "centred on the track");
                Assert.LessOrEqual(Mathf.Abs(Mathf.DeltaAngle(heading, decal.Yaw)), 6.01f, "along the track");
                var twin = context.Decals.Single(d => (d.Position + decal.Position).sqrMagnitude < 1e-4f);
                Assert.AreEqual(0f, Mathf.DeltaAngle(decal.Yaw + 180f, twin.Yaw), 1e-3f, "the copy turns with the map");
            }
        }

        [Test]
        public void ADecalBothAlongRoadAndRandomlyTurned_IsAnErrorNamingIt()
        {
            var climate = Climate(10f);
            climate.decals.Add(new MapClimateDecal { key = "Ruts", size = new Vector2(3f, 5f), alongRoad = true });
            var issues = new MapIssues();

            MapAssetCheck.Check(climate, new MapPalette(), issues);

            Assert.IsTrue(issues.Errors.Any(e => e.Contains("Ruts") && e.Contains("alongRoad")), issues.ToString());
        }

        /// <summary>The road fixture's climate plus a Muddy surface laying Puddle decals.</summary>
        private static MapClimate Climate(float perHectare)
        {
            var climate = MapRoadFixture.Climate();
            climate.decals.Add(new MapClimateDecal { key = "Puddle", size = new Vector2(1f, 2f), randomRotation = true });
            var muddy = MapClimateFixture.Surface("Muddy", "Meadow");
            muddy.decals.Add(new MapDecalDensity { decal = "Puddle", perHectare = perHectare });
            climate.surfaces.Add(muddy);
            return climate;
        }
    }
}
