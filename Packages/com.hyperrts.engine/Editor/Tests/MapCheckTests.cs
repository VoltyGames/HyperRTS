using System;
using System.Linq;
using HyperRTS.Editor.Maps;
using HyperRTS.Editor.Maps.Checks;
using HyperRTS.Editor.Maps.Data;
using HyperRTS.Editor.Maps.Spec;
using NUnit.Framework;
using UnityEngine;

namespace HyperRTS.Editor.Tests
{
    /// <summary>The checks before a build: every key a spec, kit or palette entry names resolves, or is an error naming it.</summary>
    public class MapCheckTests
    {
        [Test]
        public void AnUnknownRoadClass_IsAnErrorNamingIt()
        {
            var spec = MapRoadFixture.Spec();
            spec.roads.Add(MapRoadFixture.Road("Cobbles", new Vector2(-50f, -100f), new Vector2(-50f, -50f)));

            var issues = CheckSpec(spec);

            Assert.IsTrue(issues.Errors.Any(e => e.Contains("Road 1") && e.Contains("Cobbles")), issues.ToString());
        }

        [Test]
        public void ALineOfNeitherTreeNorProp_IsAnErrorNamingIt()
        {
            var spec = MapRoadFixture.Spec();
            spec.lines.Add(new MapLineSpec { kind = "Hedge" });

            var issues = CheckSpec(spec);

            Assert.IsTrue(issues.Errors.Any(e => e.Contains("Line 1") && e.Contains("Hedge")), issues.ToString());
        }

        [Test]
        public void AResourceWithoutAnAmount_IsAnError()
        {
            var spec = MapRoadFixture.Spec();
            spec.resources.Add(new MapResourceSpec { kind = "Oil" });

            var issues = CheckSpec(spec, "Oil");

            Assert.IsTrue(issues.Errors.Any(e => e.Contains("Resource 1") && e.Contains("amount")), issues.ToString());
        }

        [Test]
        public void ACheckedSpec_HasNoErrors()
        {
            var spec = MapRoadFixture.Spec();
            spec.roads.Add(MapRoadFixture.Road("Dirt", new Vector2(-50f, -100f), new Vector2(-50f, -50f)));

            var issues = CheckSpec(spec);

            Assert.IsFalse(issues.HasErrors, issues.ToString());
        }

        [TestCase("Cobbles", 0f, "Cobbles", TestName = "AnUnknownAccessClass_IsAnErrorNamingIt")]
        [TestCase(null, 20f, "sharedAccessDistance", TestName = "ASharedAccessDistanceWithoutAccess_IsAnErrorNamingIt")]
        [TestCase("Dirt", -1f, "sharedAccessDistance", TestName = "ANegativeSharedAccessDistance_IsAnErrorNamingIt")]
        public void ABadAccessEntry_IsAnErrorNamingIt(string access, float distance, string named)
        {
            var palette = new MapPalette();
            palette.entries.Add(new MapPaletteEntry { key = "Shed", access = access, sharedAccessDistance = distance });

            var issues = CheckAssets(MapRoadFixture.Climate(), palette);

            Assert.IsTrue(issues.Errors.Any(e => e.Contains("Shed") && e.Contains(named)), issues.ToString());
        }

        private static readonly TestCaseData[] BrokenSettlementKits =
        {
            new TestCaseData((Action<MapSettlementKit>)(kit => kit.houses[0] = "Nope"), "Nope")
                .SetName("ASettlementKeyThatDoesNotResolve_IsAnErrorNamingTheKitAndKey"),
            new TestCaseData((Action<MapSettlementKit>)(kit => kit.streetPipeArch = string.Empty), "streetPipeArch")
                .SetName("AStreetPipeWithoutItsArch_IsAnErrorNamingTheKit"),
            new TestCaseData((Action<MapSettlementKit>)(kit => kit.streetPipeMinSections = 0), "streetPipeMinSections")
                .SetName("AStreetPipeWithoutMinSections_IsAnErrorNamingTheKit"),
            new TestCaseData((Action<MapSettlementKit>)(kit => kit.gardenSurface = "Mud"), "Mud")
                .SetName("AnUnknownSettlementSurface_IsAnErrorNamingTheKitAndSurface"),
        };

        [TestCaseSource(nameof(BrokenSettlementKits))]
        public void ABrokenSettlementKit_IsAnErrorNamingTheKit(Action<MapSettlementKit> edit, string named)
        {
            var issues = MapSettlementFixture.CheckAssets(edit);

            Assert.IsTrue(issues.Errors.Any(e => e.Contains("Village") && e.Contains(named)), issues.ToString());
        }

        [Test]
        public void MissingClimateValues_AreErrorsNamingThem()
        {
            var climate = MapRoadFixture.Climate();
            climate.farmlandKits.Add(new MapFarmlandKit { key = "Farms", marginSurface = "Meadow" });

            var issues = CheckAssets(climate, new MapPalette());

            foreach (var field in new[] { "fordShallowsWidth", "fordRapidsLength", "trackJoinDistance", "macro regionLight" })
            {
                Assert.IsTrue(issues.Errors.Any(e => e.Contains(field)), $"{field}: {issues}");
            }
        }

        [Test]
        public void AWoodlandTreeWithMaxEdgeDistance_IsAnErrorNamingIt()
        {
            var climate = MapRoadFixture.Climate();
            climate.trees.Add(new MapClimateTree { key = "Oak" });
            var kit = new MapWoodlandKit { key = "Grove", surface = "Meadow", edgeSurface = "Meadow" };
            kit.edgeTrees.Add(new MapTreeDensity { tree = "Oak", maxEdgeDistance = 4f });
            climate.woodlandKits.Add(kit);

            var issues = CheckAssets(climate, new MapPalette());

            Assert.IsTrue(issues.Errors.Any(e => e.Contains("Grove") && e.Contains("maxEdgeDistance")), issues.ToString());
        }

        [Test]
        public void ABesideRoadPropOnASurface_IsAnErrorNamingIt()
        {
            var climate = MapRoadFixture.Climate();
            climate.surfaces.Single(s => s.key == "Meadow").props.Add(new MapPropDensity { kind = "Milestone" });
            var palette = new MapPalette();
            palette.entries.Add(new MapPaletteEntry { key = "Milestone", besideRoad = true });

            var issues = CheckAssets(climate, palette);

            Assert.IsTrue(issues.Errors.Any(e => e.Contains("Milestone") && e.Contains("besideRoad")), issues.ToString());
        }

        [Test]
        public void ABesideRoadSpecProp_IsAnErrorNamingIt()
        {
            var spec = MapRoadFixture.Spec();
            spec.props.Add(new MapPropSpec { kind = "Milestone" });
            var rules = new MapRules();
            rules.modes.Add(new MapModeRules { mode = spec.mode });
            var palette = new MapPalette();
            palette.entries.Add(new MapPaletteEntry { key = "Milestone", besideRoad = true });
            var issues = new MapIssues();

            MapSpecCheck.Check(spec, MapRoadFixture.Climate(), palette, rules, issues);

            Assert.IsTrue(issues.Errors.Any(e => e.Contains("Prop 1") && e.Contains("besideRoad")), issues.ToString());
        }

        private static MapIssues CheckAssets(MapClimate climate, MapPalette palette)
        {
            var issues = new MapIssues();
            MapAssetCheck.Check(climate, palette, issues);
            return issues;
        }

        /// <summary>The spec check against the road fixture's climate, a 1v1 rules asset and a palette of <paramref name="entries"/>.</summary>
        private static MapIssues CheckSpec(MapSpec spec, params string[] entries)
        {
            var rules = new MapRules();
            rules.modes.Add(new MapModeRules { mode = spec.mode });
            var palette = new MapPalette();
            foreach (var key in entries)
            {
                palette.entries.Add(new MapPaletteEntry { key = key });
            }

            var issues = new MapIssues();
            MapSpecCheck.Check(spec, MapRoadFixture.Climate(), palette, rules, issues);
            return issues;
        }
    }
}
