using System.Collections.Generic;
using System.Linq;
using HyperRTS.Editor.Maps;
using HyperRTS.Editor.Maps.Checks;
using HyperRTS.Editor.Maps.Geometry;
using HyperRTS.Editor.Maps.Layout;
using HyperRTS.Editor.Maps.Palette;
using HyperRTS.Editor.Maps.Placement;
using HyperRTS.Editor.Maps.Refine;
using HyperRTS.Editor.Maps.Spec;
using NUnit.Framework;
using UnityEngine;

namespace HyperRTS.Editor.Tests.Maps.Roads
{
    /// <summary>Roads: refining corners and joins, warnings for dead ends and tight bends, and padded props kept off them.</summary>
    public class MapRoadTests
    {
        private static List<Vector2> Refine(MapSpec spec, int road)
        {
            var symmetry = MapSymmetry.Parse(spec.symmetry);
            MapRoadRefiner.Refine(spec, MapFixture.Climate(), symmetry);
            return MapGeometry.ToVectors(spec.roads[road].points);
        }

        private static MapSpec SpecWith(params MapRoadSpec[] roads)
        {
            var spec = MapFixture.Spec();
            spec.roads.AddRange(roads);
            return spec;
        }

        [Test]
        public void Corners_BecomeArcsOfTheClassRadius_AndTheEndsStay()
        {
            var corner = new Vector2(-100f, -60f);
            var spec = SpecWith(MapFixture.Road("Asphalt", new Vector2(-100f, -115f), corner, new Vector2(-20f, -60f)));

            var line = Refine(spec, 0);

            Assert.AreEqual(-100f, line[0].x, 1e-3f);
            Assert.AreEqual(-115f, line[0].y, 1e-3f);
            Assert.AreEqual(-20f, line[^1].x, 1e-3f);
            Assert.AreEqual(-60f, line[^1].y, 1e-3f);
            Assert.Greater(MapGeometry.DistanceToPolyline(line, corner), 8f, "the corner is cut off by the arc");
            Assert.GreaterOrEqual(MapFixture.TightestRadius(line), 25f * 0.85f);
            Assert.IsTrue(line.All(p => p.y <= 0.5f), "the road stays in the authored sector");
        }

        [Test]
        public void Arcs_TakeAtMostHalfOfTheShortSegments()
        {
            var spec = SpecWith(MapFixture.Road("Asphalt", new Vector2(-100f, -100f), new Vector2(-100f, -80f),
                new Vector2(-80f, -80f)));

            var line = Refine(spec, 0);

            var radius = MapFixture.TightestRadius(line);
            Assert.Less(radius, 25f * 0.6f, "the 20 m legs cannot hold a 25 m arc");
            Assert.GreaterOrEqual(radius, 10f * 0.85f, "half a 20 m leg gives a 10 m arc");
            Assert.AreEqual(-80f, line[^1].x, 1e-3f);
            Assert.AreEqual(-80f, line[^1].y, 1e-3f);
        }

        [Test]
        public void AnEndThatStopsShort_JoinsTheRoadItMeets_AtRightAngles()
        {
            var spec = SpecWith(
                MapFixture.Road("Asphalt", new Vector2(-110f, -60f), new Vector2(-20f, -60f)),
                MapFixture.Road("Dirt", new Vector2(-60f, -115f), new Vector2(-60f, -72f)));

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
                MapFixture.Road("Asphalt", new Vector2(-110f, -60f), new Vector2(-20f, -60f)),
                MapFixture.Road("Dirt", new Vector2(-100f, -100f), new Vector2(-60f, -72f)));

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
                MapFixture.Road("Asphalt", new Vector2(-110f, -8f), new Vector2(-20f, -8f)),
                MapFixture.Road("Dirt", new Vector2(-60f, -80f), new Vector2(-60f, -0.3f)));

            var dirt = Refine(spec, 1);

            Assert.AreEqual(-60f, dirt[^1].x, 1e-3f);
            Assert.AreEqual(-0.3f, dirt[^1].y, 1e-3f, "its copy continues it across the seam");
        }

        [Test]
        public void EndsAtThePlayableEdge_AreLeftAlone()
        {
            var spec = SpecWith(
                MapFixture.Road("Asphalt", new Vector2(-112f, -60f), new Vector2(-112f, -139f)),
                MapFixture.Road("Dirt", new Vector2(-60f, -100f), new Vector2(-119f, -100f)));

            var dirt = Refine(spec, 1);

            Assert.AreEqual(-119f, dirt[^1].x, 1e-3f, "the backdrop continues it");
            Assert.AreEqual(-100f, dirt[^1].y, 1e-3f);
        }

        [Test]
        public void AnEndThatMeetsNothing_IsADeadEnd_ReportedOncePerAuthoredPlace_AgainstItsRoad()
        {
            var spec = MapFixture.Spec();
            spec.roads.Add(MapFixture.Road("Dirt", new Vector2(-60f, -100f), new Vector2(-60f, -50f)));

            var issues = Check(spec);

            var deadEnds = issues.Warnings.Where(w => w.Message.Contains("dead-ends")).ToList();
            Assert.AreEqual(2, deadEnds.Count, "two ends, copies reported once");
            Assert.AreEqual(new MapIssueFeature(nameof(MapSpec.roads), 0), deadEnds[0].Feature);
        }

        [Test]
        public void EdgeSeamSettlementAndJoinedEnds_AreNotDeadEnds()
        {
            var spec = MapFixture.Spec();
            spec.roads.Add(MapFixture.Road("Asphalt", new Vector2(-119f, -60f), new Vector2(-1f, -0.3f)));
            spec.roads.Add(MapFixture.Road("Dirt", new Vector2(-60f, -110f), new Vector2(-60f, -30.15f)));
            spec.settlements.Add(new MapSettlementSpec { center = new MapPoint(-60f, -110f), radius = 15f, kind = "Village" });

            var issues = Check(spec);

            Assert.IsEmpty(issues.Warnings, issues.ToString());
        }

        [Test]
        public void ASharpCorner_IsFlagged_AndRefiningClearsIt()
        {
            var corner = new[] { new Vector2(-100f, -100f), new Vector2(-100f, -60f), new Vector2(-60f, -60f) };
            var sharp = SpecWith(MapFixture.Road("Asphalt", corner));
            var refined = SpecWith(MapFixture.Road("Asphalt", corner));
            MapRoadRefiner.Refine(refined, MapFixture.Climate(), MapSymmetry.Parse(refined.symmetry));

            Assert.IsTrue(Check(sharp).Warnings.Any(w => w.Message.Contains("bends")));
            Assert.IsFalse(Check(refined).Warnings.Any(w => w.Message.Contains("bends")));
        }

        [TestCase(1f, true, TestName = "APaddedPropOnARoad_IsAnErrorNamingIt")]
        [TestCase(12f, false, TestName = "APaddedPropBesideTheRoad_IsFine")]
        public void PaddedPropsStayOffRoads(float x, bool onRoad)
        {
            var spec = SpecWith(MapFixture.Road("Dirt", new Vector2(0f, -120f), new Vector2(0f, 0f)));
            spec.props.Add(new MapPropSpec { pos = new MapPoint(x, -40f), kind = "Pumpjack" });
            var climate = MapFixture.Climate();
            var layout = MapFixture.Layout(spec);
            var palette = MapFixture.Item<MapPalette>("Palette", i => { i.baseRadius = 15f; });
            palette.entries.Add(MapFixture.Item<MapProp>("Pumpjack", i => { i.size = new Vector2(8f, 16f); i.pad = "Dirt"; }));
            var issues = new MapIssues();

            MapPropPlacer.Place(layout, new MapShapes(layout, palette, climate), palette, climate, issues);

            Assert.AreEqual(onRoad, issues.HasErrors, issues.ToString());
            Assert.AreEqual(onRoad, issues.ToString().Contains("'Pumpjack' at (1, -40) stands on a road"));
        }

        private static MapIssues Check(MapSpec spec)
        {
            var layout = MapFixture.Layout(spec);
            var issues = new MapIssues();
            MapRoadChecks.Check(layout, MapFixture.Shapes(layout, MapFixture.Climate()), 20f, issues);
            return issues;
        }

        /// <summary>Acute angle (degrees) between the road's last 3 m and the horizontal asphalt it joins.</summary>
        private static float Approach(List<Vector2> line)
        {
            var direction = (line[^1] - line[^2]).normalized;
            return Mathf.Acos(Mathf.Abs(direction.x)) * Mathf.Rad2Deg;
        }
    }
}
