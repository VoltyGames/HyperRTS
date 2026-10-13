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

        [Test]
        public void AnUnknownAccessClass_IsAnErrorNamingIt()
        {
            var palette = new MapPalette();
            palette.entries.Add(new MapPaletteEntry { key = "Shed", access = "Cobbles" });
            var issues = new MapIssues();

            MapAssetCheck.Check(MapRoadFixture.Climate(), palette, issues);

            Assert.IsTrue(issues.Errors.Any(e => e.Contains("Shed") && e.Contains("Cobbles")), issues.ToString());
        }

        [Test]
        public void ASettlementKeyThatDoesNotResolve_IsAnErrorNamingTheKitAndKey()
        {
            var issues = MapSettlementFixture.CheckAssets(kit => kit.houses[0] = "Nope");

            Assert.IsTrue(issues.Errors.Any(e => e.Contains("Village") && e.Contains("Nope")), issues.ToString());
        }

        [Test]
        public void AStreetPipeWithoutItsArch_IsAnErrorNamingTheKit()
        {
            var issues = MapSettlementFixture.CheckAssets(kit => kit.streetPipeArch = string.Empty);

            Assert.IsTrue(issues.Errors.Any(e => e.Contains("Village") && e.Contains("streetPipeArch")), issues.ToString());
        }

        [Test]
        public void AStreetPipeWithoutAShortestRun_IsAnErrorNamingTheKit()
        {
            var issues = MapSettlementFixture.CheckAssets(kit => kit.streetPipeMinRun = 0);

            Assert.IsTrue(issues.Errors.Any(e => e.Contains("Village") && e.Contains("streetPipeMinRun")), issues.ToString());
        }

        [Test]
        public void AnUnknownSettlementSurface_IsAnErrorNamingTheKitAndSurface()
        {
            var issues = MapSettlementFixture.CheckAssets(kit => kit.gardenSurface = "Mud");

            Assert.IsTrue(issues.Errors.Any(e => e.Contains("Village") && e.Contains("Mud")), issues.ToString());
        }

        [Test]
        public void AnAccessGroupReachWithoutAccess_IsAnErrorNamingIt()
        {
            var palette = new MapPalette();
            palette.entries.Add(new MapPaletteEntry { key = "Shed", accessGroupReach = 20f });

            var issues = CheckAssets(MapRoadFixture.Climate(), palette);

            Assert.IsTrue(issues.Errors.Any(e => e.Contains("Shed") && e.Contains("accessGroupReach")), issues.ToString());
        }

        [Test]
        public void ANegativeAccessGroupReach_IsAnErrorNamingIt()
        {
            var palette = new MapPalette();
            palette.entries.Add(new MapPaletteEntry { key = "Shed", access = "Dirt", accessGroupReach = -1f });

            var issues = CheckAssets(MapRoadFixture.Climate(), palette);

            Assert.IsTrue(issues.Errors.Any(e => e.Contains("Shed") && e.Contains("accessGroupReach")), issues.ToString());
        }

        [Test]
        public void MissingRiverAndFarmlandDistances_AreErrorsNamingThem()
        {
            var climate = MapRoadFixture.Climate();
            climate.farmlandKits.Add(new MapFarmlandKit { key = "Farms", marginSurface = "Meadow" });

            var issues = CheckAssets(climate, new MapPalette());

            foreach (var field in new[] { "fordFade", "fordFastRun", "trackSnapReach" })
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
        public void AnAlignToRoadPropOnASurface_IsAnErrorNamingIt()
        {
            var climate = MapRoadFixture.Climate();
            climate.surfaces.Single(s => s.key == "Meadow").props.Add(new MapPropDensity { kind = "Milestone" });
            var palette = new MapPalette();
            palette.entries.Add(new MapPaletteEntry { key = "Milestone", alignToRoad = true });

            var issues = CheckAssets(climate, palette);

            Assert.IsTrue(issues.Errors.Any(e => e.Contains("Milestone") && e.Contains("alignToRoad")), issues.ToString());
        }

        [Test]
        public void AnAlignToRoadSpecProp_IsAnErrorNamingIt()
        {
            var spec = MapRoadFixture.Spec();
            spec.props.Add(new MapPropSpec { kind = "Milestone" });
            var rules = new MapRules();
            rules.modes.Add(new MapModeRules { mode = spec.mode });
            var palette = new MapPalette();
            palette.entries.Add(new MapPaletteEntry { key = "Milestone", alignToRoad = true });
            var issues = new MapIssues();

            MapSpecCheck.Check(spec, MapRoadFixture.Climate(), palette, rules, issues);

            Assert.IsTrue(issues.Errors.Any(e => e.Contains("Prop 1") && e.Contains("alignToRoad")), issues.ToString());
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
