using System.Linq;
using HyperRTS.Editor.Maps.Layout;
using HyperRTS.Editor.Maps.Report;
using HyperRTS.Editor.Maps.Spec;
using HyperRTS.Editor.Maps.Symmetry;
using NUnit.Framework;
using UnityEngine;

namespace HyperRTS.Editor.Tests
{
    /// <summary>Road warnings: dead ends and bends tighter than half the class radius, once per authored place.</summary>
    public class MapRoadChecksTests
    {
        private static MapIssues Check(MapSpec spec, bool refine = false)
        {
            var climate = MapRoadFixture.Climate();
            if (refine)
            {
                MapRoadRefiner.Refine(spec, climate, MapSymmetry.Parse(spec.symmetry), new MapIssues());
            }

            var layout = MapRoadFixture.Layout(spec);
            var shapes = MapRoadFixture.Shapes(layout, climate);
            var issues = new MapIssues();
            MapRoadChecks.Check(layout, shapes, issues);
            return issues;
        }

        [Test]
        public void AnEndThatMeetsNothing_IsADeadEnd_ReportedOncePerAuthoredPlace()
        {
            var spec = MapRoadFixture.Spec();
            spec.roads.Add(MapRoadFixture.Road("Dirt", new Vector2(-60f, -100f), new Vector2(-60f, -50f)));

            var issues = Check(spec);

            Assert.AreEqual(2, issues.Warnings.Count(w => w.Contains("dead-ends")), "two ends, copies reported once");
        }

        [Test]
        public void EdgeSeamSettlementAndJoinedEnds_AreNotDeadEnds()
        {
            var spec = MapRoadFixture.Spec();
            spec.roads.Add(MapRoadFixture.Road("Asphalt", new Vector2(-119f, -60f), new Vector2(-1f, -0.3f)));
            spec.roads.Add(MapRoadFixture.Road("Dirt", new Vector2(-60f, -110f), new Vector2(-60f, -30.15f)));
            spec.settlements.Add(new MapSettlementSpec { center = new MapPoint(-60f, -110f), radius = 15f, kind = "Village" });

            var issues = Check(spec);

            Assert.IsEmpty(issues.Warnings, issues.ToString());
        }

        [Test]
        public void ASharpCorner_IsFlagged_AndRefiningClearsIt()
        {
            var corner = new[] { new Vector2(-100f, -100f), new Vector2(-100f, -60f), new Vector2(-60f, -60f) };
            var sharp = MapRoadFixture.Spec();
            sharp.roads.Add(MapRoadFixture.Road("Asphalt", corner));
            var refined = MapRoadFixture.Spec();
            refined.roads.Add(MapRoadFixture.Road("Asphalt", corner));

            Assert.IsTrue(Check(sharp).Warnings.Any(w => w.Contains("bends")));
            Assert.IsFalse(Check(refined, true).Warnings.Any(w => w.Contains("bends")));
        }
    }
}
