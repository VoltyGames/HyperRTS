using System.Collections.Generic;
using System.Linq;
using HyperRTS.Editor.Maps;
using HyperRTS.Editor.Maps.Climate;
using HyperRTS.Editor.Maps.Land;
using HyperRTS.Editor.Maps.Palette;
using HyperRTS.Editor.Maps.Placement;
using HyperRTS.Editor.Maps.Planning;
using HyperRTS.Editor.Maps.Planning.War;
using NUnit.Framework;
using UnityEngine;

namespace HyperRTS.Editor.Tests.Maps.Ground
{
    /// <summary>War age scars: none on an untouched map, the same in every copy, and clear of roads and bases.</summary>
    public class MapWarScarsTests
    {
        private const float BeltZ = -60f;

        [Test]
        public void AnUntouchedMap_ScarsNothing()
        {
            var context = Planned(nameof(MapWarAge.None));

            Assert.IsEmpty(context.WarScars.Craters);
            Assert.IsEmpty(context.WarScars.Burns);
            Assert.IsEmpty(context.Decals);
            Assert.IsFalse(Wrecks(context).Any());
        }

        [Test]
        public void Scars_AreTheSameInEveryCopy()
        {
            var context = Planned(nameof(MapWarAge.Frontline));
            var scars = context.WarScars;

            Assert.Greater(scars.Craters.Count, 4);
            Assert.Greater(scars.Burns.Count, 2);
            Assert.Greater(Wrecks(context).Count(), 2);
            foreach (var crater in scars.Craters)
            {
                var twin = scars.Craters.Single(c => (c.Center + crater.Center).sqrMagnitude < 1e-4f);
                Assert.AreEqual(crater.Radius, twin.Radius);
                Assert.AreEqual(crater.Depth, twin.Depth);
                Assert.AreEqual(context.Heights.Height(crater.Center), context.Heights.Height(twin.Center), 0.05f);
            }

            foreach (var patch in scars.Burns)
            {
                Assert.AreEqual(1, scars.Burns.Count(b => (b.Center + patch.Center).sqrMagnitude < 1e-4f));
            }

            foreach (var wreck in Wrecks(context))
            {
                Assert.AreEqual(1, Wrecks(context).Count(w => (w.Position + wreck.Position).sqrMagnitude < 1e-4f));
            }
        }

        [Test]
        public void Scars_KeepOffRoadsAndBases()
        {
            var context = Planned(nameof(MapWarAge.Frontline));
            var shapes = context.Shapes;
            var bases = context.Layout.Spawns.Select(s => s.Position).ToList();
            var baseRadius = context.Palette.baseRadius;

            foreach (var crater in context.WarScars.Craters)
            {
                Assert.Greater(shapes.RoadEdgeDistance(crater.Center), crater.Reach);
                Assert.IsTrue(bases.All(b => Vector2.Distance(b, crater.Center) > baseRadius + crater.Reach));
            }

            foreach (var patch in context.WarScars.Burns)
            {
                Assert.Greater(shapes.RoadEdgeDistance(patch.Center), patch.Radius * 0.5f);
                Assert.IsTrue(bases.All(b => Vector2.Distance(b, patch.Center) > baseRadius));
            }

            foreach (var wreck in Wrecks(context))
            {
                Assert.Greater(shapes.RoadEdgeDistance(wreck.Position), wreck.Radius);
                Assert.LessOrEqual(Mathf.Abs(Mathf.Abs(wreck.Position.y) - Mathf.Abs(BeltZ)), 8f + 4f + wreck.Radius, "near the belt");
            }
        }

        /// <summary>A meadow map with an asphalt road and a belt in each half, planned at the given war age.</summary>
        private static MapPlanContext Planned(string warAge)
        {
            var spec = MapFixture.Spec();
            spec.warAge = warAge;
            var context = MapFixture.Context(spec, Climate(), new MapIssues());
            context.Palette.entries.Add(MapFixture.Item<MapProp>("Wreck", i => { i.size = new Vector2(3f, 6f); }));
            var asphalt = context.Climate.RoadClass("Asphalt");
            context.Shapes.AddRoad(MapFixture.RoadShape(asphalt, false, new Vector2(-120f, -20f), new Vector2(120f, -20f)));
            context.Shapes.AddRoad(MapFixture.RoadShape(asphalt, false, new Vector2(120f, 20f), new Vector2(-120f, 20f)));
            var land = context.Land;
            var belt = context.Region(MapLandKind.Belt, "Meadow");
            land.ClaimWhere(land.Bounds, belt, p => Mathf.Abs(Mathf.Abs(p.y) - Mathf.Abs(BeltZ)) < 4f);
            land.ClaimWhere(land.Bounds, context.Region(MapLandKind.Meadow, "Meadow"), _ => true);

            new MapWarScarsPlanner(context).Plan();
            return context;
        }

        private static IEnumerable<MapPlacement> Wrecks(MapPlanContext context) =>
            context.Placements.All.Where(p => p.Entry.key == "Wreck");

        private static MapClimate Climate()
        {
            var climate = MapFixture.Climate();
            climate.layers.Add(MapFixture.Item<MapClimateLayer>("Burnt"));
            climate.decals.Add(MapFixture.Item<MapClimateDecal>("Crater"));
            climate.decals.Add(MapFixture.Item<MapClimateDecal>("Puddle"));
            var dense = new MapWarDensity { cratersPerHectare = 10f, burnsPerHectare = 2f, wrecksPerHectare = 2f };
            climate.war = MapFixture.Item<MapWarKit>("Test", i =>
            {
                i.frontline = dense;
                i.craterDecal = "Crater";
                i.wetDecal = "Puddle";
                i.burntLayer = "Burnt";
                i.wrecks.AddRange(new[] { "Wreck" });
            });
            return climate;
        }
    }
}
