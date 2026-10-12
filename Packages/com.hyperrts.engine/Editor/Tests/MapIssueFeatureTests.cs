using System.Linq;
using HyperRTS.Editor.Maps;
using HyperRTS.Editor.Maps.Checks;
using HyperRTS.Editor.Maps.Data;
using HyperRTS.Editor.Maps.Layout;
using HyperRTS.Editor.Maps.Report;
using HyperRTS.Editor.Maps.Spec;
using NUnit.Framework;
using UnityEngine;

namespace HyperRTS.Editor.Tests
{
    /// <summary>Issues name the spec feature they are about, so the Map Editor can select it, and the report keeps that.</summary>
    public class MapIssueFeatureTests
    {
        [Test]
        public void ASpecCheckError_NamesTheFeature()
        {
            var spec = MapRoadFixture.Spec();
            spec.roads.Add(MapRoadFixture.Road("Dirt", new Vector2(-50f, -100f), new Vector2(-50f, -50f)));
            spec.roads.Add(MapRoadFixture.Road("Cobbles", new Vector2(-60f, -100f), new Vector2(-60f, -50f)));
            var rules = new MapRules();
            rules.modes.Add(new MapModeRules { mode = spec.mode });
            var issues = new MapIssues();

            MapSpecCheck.Check(spec, MapRoadFixture.Climate(), new MapPalette(), rules, issues);

            AssertFeature(issues, "Cobbles", new MapIssueFeature(nameof(MapSpec.roads), 1));
        }

        [Test]
        public void AnExpanderError_NamesTheFeature()
        {
            var spec = MapRoadFixture.Spec();
            spec.props.Add(new MapPropSpec { pos = new MapPoint(10f, 30f), kind = "Derrick" });
            var issues = new MapIssues();

            MapExpander.Expand(spec, issues);

            AssertFeature(issues, "outside the authored sector", new MapIssueFeature(nameof(MapSpec.props), 0));
        }

        [Test]
        public void ARoadWarning_NamesTheAuthoredRoad()
        {
            var spec = MapRoadFixture.Spec();
            spec.roads.Add(MapRoadFixture.Road("Dirt", new Vector2(-60f, -100f), new Vector2(-60f, -50f)));
            var climate = MapRoadFixture.Climate();
            var layout = MapRoadFixture.Layout(spec);
            var issues = new MapIssues();

            MapRoadChecks.Check(layout, MapRoadFixture.Shapes(layout, climate), 20f, issues);

            var index = issues.Warnings.ToList().FindIndex(w => w.Contains("dead-ends"));
            Assert.GreaterOrEqual(index, 0, issues.ToString());
            Assert.AreEqual(new MapIssueFeature(nameof(MapSpec.roads), 0), issues.WarningFeatures[index]);
        }

        [Test]
        public void TheReport_KeepsEachIssuesFeature_ThroughJson()
        {
            var issues = new MapIssues();
            issues.Error("map-wide");
            issues.Warn("about a road", new MapIssueFeature(nameof(MapSpec.roads), 2));
            var spec = MapRoadFixture.Spec();

            var json = JsonUtility.ToJson(MapReport.Create(spec, issues, new MapMetrics()));
            var report = JsonUtility.FromJson<MapReport>(json);

            Assert.IsTrue(report.errorFeatures.Single().IsNone);
            Assert.AreEqual(new MapIssueFeature(nameof(MapSpec.roads), 2), report.warningFeatures.Single());
            Assert.AreEqual("about a road", report.warnings.Single(), "the messages stay a plain list");
        }

        private static void AssertFeature(MapIssues issues, string text, MapIssueFeature expected)
        {
            var index = issues.Errors.ToList().FindIndex(e => e.Contains(text));
            Assert.GreaterOrEqual(index, 0, issues.ToString());
            Assert.AreEqual(expected, issues.ErrorFeatures[index]);
        }
    }
}
