using System.Collections.Generic;
using System.Linq;
using HyperRTS.Editor.Maps.Geometry;
using HyperRTS.Editor.Maps.Layout;
using HyperRTS.Editor.Maps.Spec;
using HyperRTS.Editor.Maps.Symmetry;
using NUnit.Framework;
using UnityEngine;

namespace HyperRTS.Editor.Tests
{
    /// <summary>Road refining: corner arcs, kept ends, joins at a good angle, and ends left alone on the seam and edge.</summary>
    public class MapRoadRefinerTests
    {
        private static List<Vector2> Refine(MapSpec spec, int road, MapIssues issues = null)
        {
            var symmetry = MapSymmetry.Parse(spec.symmetry);
            MapRoadRefiner.Refine(spec, MapRoadFixture.Climate(), symmetry, issues ?? new MapIssues());
            return MapGeometry.ToVectors(spec.roads[road].points);
        }

        private static MapSpec SpecWith(params MapRoadSpec[] roads)
        {
            var spec = MapRoadFixture.Spec();
            spec.roads.AddRange(roads);
            return spec;
        }

        [Test]
        public void Corners_BecomeArcsOfTheClassRadius_AndTheEndsStay()
        {
            var corner = new Vector2(-100f, -60f);
            var spec = SpecWith(MapRoadFixture.Road("Asphalt", new Vector2(-100f, -115f), corner, new Vector2(-20f, -60f)));

            var line = Refine(spec, 0);

            Assert.AreEqual(-100f, line[0].x, 1e-3f);
            Assert.AreEqual(-115f, line[0].y, 1e-3f);
            Assert.AreEqual(-20f, line[^1].x, 1e-3f);
            Assert.AreEqual(-60f, line[^1].y, 1e-3f);
            Assert.Greater(MapGeometry.DistanceToPolyline(line, corner), 8f, "the corner is cut off by the arc");
            Assert.GreaterOrEqual(MapRoadFixture.TightestRadius(line), 25f * 0.85f);
            Assert.IsTrue(line.All(p => p.y <= 0.5f), "the road stays in the authored sector");
        }

        [Test]
        public void Arcs_TakeAtMostHalfOfTheShortSegments()
        {
            var spec = SpecWith(MapRoadFixture.Road("Asphalt", new Vector2(-100f, -100f), new Vector2(-100f, -80f),
                new Vector2(-80f, -80f)));

            var line = Refine(spec, 0);

            var radius = MapRoadFixture.TightestRadius(line);
            Assert.Less(radius, 25f * 0.6f, "the 20 m legs cannot hold a 25 m arc");
            Assert.GreaterOrEqual(radius, 10f * 0.85f, "half a 20 m leg gives a 10 m arc");
            Assert.AreEqual(-80f, line[^1].x, 1e-3f);
            Assert.AreEqual(-80f, line[^1].y, 1e-3f);
        }

        [Test]
        public void AnEndThatStopsShort_JoinsTheRoadItMeets_AtRightAngles()
        {
            var spec = SpecWith(
                MapRoadFixture.Road("Asphalt", new Vector2(-110f, -60f), new Vector2(-20f, -60f)),
                MapRoadFixture.Road("Dirt", new Vector2(-60f, -115f), new Vector2(-60f, -72f)));

            var dirt = Refine(spec, 1);

            Assert.AreEqual(-60f, dirt[0].x, 1e-3f, "the far end stays");
            Assert.AreEqual(-115f, dirt[0].y, 1e-3f);
            Assert.AreEqual(-60f, dirt[^1].x, 0.05f);
            Assert.AreEqual(-60f, dirt[^1].y, 0.05f, "the end snaps onto the asphalt centre line");
            Assert.GreaterOrEqual(Approach(dirt), 60f);
        }

        [Test]
        public void AShallowJoin_BendsIntoAnApproachOfSixtyDegrees()
        {
            var spec = SpecWith(
                MapRoadFixture.Road("Asphalt", new Vector2(-110f, -60f), new Vector2(-20f, -60f)),
                MapRoadFixture.Road("Dirt", new Vector2(-100f, -100f), new Vector2(-60f, -72f)));

            var dirt = Refine(spec, 1);

            Assert.AreEqual(-100f, dirt[0].x, 1e-3f);
            Assert.AreEqual(-60f, dirt[^1].y, 0.05f, "the end lands on the asphalt");
            Assert.GreaterOrEqual(Approach(dirt), 55f, "the last stretch turns toward the road");
            Assert.IsTrue(dirt.All(p => p.y <= 0.5f));
        }

        [Test]
        public void EndsOnTheSeam_AreLeftAlone()
        {
            var spec = SpecWith(
                MapRoadFixture.Road("Asphalt", new Vector2(-110f, -8f), new Vector2(-20f, -8f)),
                MapRoadFixture.Road("Dirt", new Vector2(-60f, -80f), new Vector2(-60f, -0.3f)));

            var dirt = Refine(spec, 1);

            Assert.AreEqual(-60f, dirt[^1].x, 1e-3f);
            Assert.AreEqual(-0.3f, dirt[^1].y, 1e-3f, "its copy continues it across the seam");
        }

        [Test]
        public void EndsAtThePlayableEdge_AreLeftAlone()
        {
            var spec = SpecWith(
                MapRoadFixture.Road("Asphalt", new Vector2(-112f, -60f), new Vector2(-112f, -139f)),
                MapRoadFixture.Road("Dirt", new Vector2(-60f, -100f), new Vector2(-119f, -100f)));

            var dirt = Refine(spec, 1);

            Assert.AreEqual(-119f, dirt[^1].x, 1e-3f, "the margin continues it");
            Assert.AreEqual(-100f, dirt[^1].y, 1e-3f);
        }

        [Test]
        public void AnUnknownRoadClass_IsAnErrorNamingIt()
        {
            var spec = SpecWith(MapRoadFixture.Road("Cobbles", new Vector2(-50f, -100f), new Vector2(-50f, -50f)));
            var issues = new MapIssues();

            Refine(spec, 0, issues);

            Assert.IsTrue(issues.Errors.Any(e => e.Contains("Cobbles")));
        }

        /// <summary>Acute angle (degrees) between the road's last 3 m and the horizontal asphalt it joins.</summary>
        private static float Approach(List<Vector2> line)
        {
            var direction = (line[^1] - line[^2]).normalized;
            return Mathf.Acos(Mathf.Abs(direction.x)) * Mathf.Rad2Deg;
        }
    }
}
