using System.Collections.Generic;
using System.Linq;
using HyperRTS.Editor.Maps;
using HyperRTS.Editor.Maps.Data;
using HyperRTS.Editor.Maps.Land;
using HyperRTS.Editor.Maps.Placement;
using HyperRTS.Editor.Maps.Planning;
using HyperRTS.Editor.Maps.Planning.Farmland;
using HyperRTS.Editor.Maps.Spec;
using NUnit.Framework;
using UnityEngine;

namespace HyperRTS.Editor.Tests.Maps.Roads
{
    /// <summary>Access and field tracks: from a pad or field end to the nearest road they can reach, in every copy.</summary>
    public class MapAccessTests
    {
        private static readonly Vector2 Prop = new(20f, -100f);

        private static MapRoadSpec MainRoad(float z) =>
            MapFixture.Road("Asphalt", new Vector2(-110f, z), new Vector2(110f, z));

        private static MapRavineSpec Ravine(float z, float from, float to) => new()
        {
            points = new List<MapPoint> { new(from, z), new(to, z) }, width = 12f, depth = 3f, rim = 4f,
        };

        /// <summary>A context for the road specs and ravines, with a "Shed" prop at <see cref="Prop"/> asking for a track.</summary>
        private static MapPlanContext Context(MapIssues issues, string access, MapRoadSpec[] roads,
            params MapRavineSpec[] ravines)
        {
            var spec = MapFixture.Spec();
            spec.roads.AddRange(roads);
            spec.ravines.AddRange(ravines);
            var context = MapFixture.Context(spec, MapFixture.Climate(), issues);
            var entry = MapFixture.Item<MapProp>("Shed", i =>
            {
                i.size = new Vector2(6f, 6f);
                i.pad = "Dirt";
                i.padMargin = 3f;
                i.access = access;
                i.sharedAccessDistance = 20f;
            });
            context.Placements.All.Add(new MapPlacement(entry, Prop, 0f, 1f));
            return context;
        }

        [Test]
        public void ATrackRunsFromThePadEdgeToTheNearestRoad_InEveryCopy()
        {
            var issues = new MapIssues();
            var context = Context(issues, "Dirt", new[] { MainRoad(-60f) });

            new MapAccessPlanner(context).Plan();

            var tracks = context.Shapes.Roads.Where(r => r.Generated).ToList();
            Assert.AreEqual(2, tracks.Count, "one track, and its copy");
            var line = tracks[0].Line.Points;
            Assert.AreEqual(4f, (line[0] - Prop).magnitude, 0.1f, "starts 2 m inside the 6 m pad edge");
            Assert.AreEqual(-60f, line[^1].y, 0.2f, "ends on the road's centre line");
            Assert.AreEqual(20f, line[^1].x, 1.5f);
            Assert.AreEqual(MapLandKind.Road, context.Land.KindAt(new Vector2(20f, -80f)));
            var copy = tracks[1].Line.Points;
            Assert.AreEqual(-line[0].x, copy[0].x, 1e-3f, "the copy is the track turned about the centre");
            Assert.AreEqual(-line[0].y, copy[0].y, 1e-3f);
            Assert.IsEmpty(issues.Warnings);
        }

        [Test]
        public void PropsWithinSharedAccessDistanceOfEachOther_ShareOneTrack()
        {
            var issues = new MapIssues();
            var context = Context(issues, "Dirt", new[] { MainRoad(-60f) });
            var shed = context.Placements.All[^1].Entry;
            foreach (var x in new[] { 35f, 50f, 90f })
            {
                context.Placements.All.Add(new MapPlacement(shed, new Vector2(x, -100f), 0f, 1f));
            }

            new MapAccessPlanner(context).Plan();

            var tracks = context.Shapes.Roads.Where(r => r.Generated).ToList();
            Assert.AreEqual(4, tracks.Count, "one track for the chain at 20, 35 and 50, one for the shed at 90, and copies");
            Assert.AreEqual(20f, tracks[0].Line.Points[0].x, 0.5f, "the chain's track leaves its first shed");
            Assert.AreEqual(90f, tracks[2].Line.Points[0].x, 0.5f);
            Assert.IsEmpty(issues.Warnings);
        }

        [Test]
        public void ARavineInTheWay_SendsTheTrackToTheNextRoad()
        {
            var issues = new MapIssues();
            var far = MapFixture.Road("Dirt", new Vector2(-90f, -118f), new Vector2(-90f, -85f));
            var context = Context(issues, "Dirt", new[] { MainRoad(-60f), far }, Ravine(-80f, -5f, 60f));

            new MapAccessPlanner(context).Plan();

            var track = context.Shapes.Roads.First(r => r.Generated);
            Assert.AreEqual(-90f, track.Line.Points[^1].x, 1.5f, "the dirt road beyond the ravine");
            Assert.IsEmpty(issues.Warnings);
        }

        [Test]
        public void NoClearRoad_IsAWarningNamingTheProp()
        {
            var issues = new MapIssues();
            var context = Context(issues, "Dirt", new[] { MainRoad(-60f) }, Ravine(-80f, -130f, 130f));

            new MapAccessPlanner(context).Plan();

            Assert.IsFalse(context.Shapes.Roads.Any(r => r.Generated));
            Assert.IsTrue(issues.Warnings.Any(w => w.Contains("Shed")), issues.ToString());
        }

        [Test]
        public void APadBesideTheRoad_NeedsNoTrack()
        {
            var issues = new MapIssues();
            var context = Context(issues, "Dirt", new[] { MainRoad(-108f) });

            new MapAccessPlanner(context).Plan();

            Assert.IsFalse(context.Shapes.Roads.Any(r => r.Generated));
            Assert.IsEmpty(issues.Warnings);
        }

        [Test]
        public void APropWithZeroSharedAccessDistance_KeepsATrackOfItsOwn()
        {
            var stack = MapFixture.Item<MapProp>("Stack", i => { i.access = "Dirt"; i.sharedAccessDistance = 20f; });
            var derrick = MapFixture.Item<MapProp>("Derrick", i => { i.access = "Dirt"; i.sharedAccessDistance = 0f; });
            var props = new List<MapPlacement>
            {
                new(stack, Vector2.zero, 0f, 1f), new(derrick, new Vector2(5f, 0f), 0f, 1f),
            };

            Assert.AreEqual(2, MapAccessGroups.Group(props).Count);
        }

        [Test]
        public void AFieldRunEndingBesideARoad_IsCarriedOntoIt()
        {
            var run = new List<Vector2> { new(40f, -67f), new(40f, -95f) };

            Assert.IsTrue(FieldLinks().Link(run));

            Assert.AreEqual(3, run.Count, "the road point is added before the start");
            Assert.AreEqual(-60f, run[0].y, 0.6f, "on the road's centre line");
            Assert.AreEqual(40f, run[0].x, 1.5f);
            Assert.AreEqual(new Vector2(40f, -95f), run[^1], "the field end stays");
        }

        [Test]
        public void AFieldRunThatReachesNoRoad_IsDropped_AndOneToTheMapEdgeLeadsSomewhere()
        {
            var nowhere = new List<Vector2> { new(60f, -80f), new(60f, -100f) };
            var toEdge = new List<Vector2> { new(-80f, -95f), new(-120f, -95f) };

            Assert.IsFalse(FieldLinks().Link(nowhere), "16 m off the road edge is out of reach");
            Assert.AreEqual(2, nowhere.Count, "nothing snapped");
            Assert.IsTrue(FieldLinks().Link(toEdge));
            Assert.AreEqual(2, toEdge.Count);
        }

        /// <summary>Field links over one asphalt road (8 m) along z = -60.</summary>
        private static MapFieldTrackLinks FieldLinks()
        {
            var spec = MapFixture.Spec();
            spec.roads.Add(MainRoad(-60f));
            return new MapFieldTrackLinks(MapFixture.Context(spec, MapFixture.Climate(), new MapIssues()), 8f);
        }
    }
}
