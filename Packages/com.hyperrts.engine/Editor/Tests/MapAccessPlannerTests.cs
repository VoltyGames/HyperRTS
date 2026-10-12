using System.Collections.Generic;
using System.Linq;
using HyperRTS.Editor.Maps;
using HyperRTS.Editor.Maps.Data;
using HyperRTS.Editor.Maps.Land;
using HyperRTS.Editor.Maps.Placement;
using HyperRTS.Editor.Maps.Planning;
using HyperRTS.Editor.Maps.Spec;
using NUnit.Framework;
using UnityEngine;

namespace HyperRTS.Editor.Tests
{
    /// <summary>Access tracks: from a prop's pad to the nearest road it can reach, in every copy of the sector.</summary>
    public class MapAccessPlannerTests
    {
        private static readonly Vector2 Prop = new(20f, -100f);

        private static MapRoadSpec MainRoad(float z) =>
            MapRoadFixture.Road("Asphalt", new Vector2(-110f, z), new Vector2(110f, z));

        private static MapRavineSpec Ravine(float z, float from, float to) => new()
        {
            points = new List<MapPoint> { new(from, z), new(to, z) }, width = 12f, depth = 3f, rim = 4f,
        };

        /// <summary>A context for the road specs and ravines, with a "Shed" prop at <see cref="Prop"/> asking for a track.</summary>
        private static MapPlanContext Context(MapIssues issues, string access, MapRoadSpec[] roads,
            params MapRavineSpec[] ravines)
        {
            var spec = MapRoadFixture.Spec();
            spec.roads.AddRange(roads);
            spec.ravines.AddRange(ravines);
            var context = MapRoadFixture.Context(spec, MapRoadFixture.Climate(), issues);
            var entry = new MapPaletteEntry
            {
                key = "Shed", size = new Vector2(6f, 6f), pad = "Dirt", padMargin = 3f, access = access,
            };
            context.Placements.All.Add(new MapPlacement(entry, Prop, 0f));
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
        public void ARavineInTheWay_SendsTheTrackToTheNextRoad()
        {
            var issues = new MapIssues();
            var far = MapRoadFixture.Road("Dirt", new Vector2(-90f, -118f), new Vector2(-90f, -85f));
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
    }
}
