using System.Linq;
using HyperRTS.Editor.Maps;
using HyperRTS.Editor.Maps.Checks;
using HyperRTS.Editor.Maps.Data;
using HyperRTS.Editor.Maps.Library;
using HyperRTS.Editor.Maps.Placement;
using HyperRTS.Editor.Maps.Report;
using HyperRTS.Editor.Maps.Spec;
using HyperRTS.Simulation.Combat;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HyperRTS.Editor.Tests.Maps
{
    /// <summary>The checks before a build name every bad key and feature, and the report keeps what they found.</summary>
    public class MapCheckTests
    {
        [Test]
        public void AnUnknownRoadClass_IsAnErrorNamingItAndItsFeature()
        {
            var spec = MapFixture.Spec();
            spec.roads.Add(MapFixture.Road("Dirt", new Vector2(-60f, -100f), new Vector2(-60f, -50f)));
            spec.roads.Add(MapFixture.Road("Cobbles", new Vector2(-50f, -100f), new Vector2(-50f, -50f)));

            var issues = CheckSpec(spec);

            var index = issues.Errors.ToList().FindIndex(e => e.Contains("Road 2") && e.Contains("Cobbles"));
            Assert.GreaterOrEqual(index, 0, issues.ToString());
            Assert.AreEqual(new MapIssueFeature(nameof(MapSpec.roads), 1), issues.ErrorFeatures[index]);
        }

        [Test]
        public void ALineOfNeitherTreeNorProp_IsAnErrorNamingIt()
        {
            var spec = MapFixture.Spec();
            spec.lines.Add(new MapLineSpec { kind = "Hedge" });

            var issues = CheckSpec(spec);

            Assert.IsTrue(issues.Errors.Any(e => e.Contains("Line 1") && e.Contains("Hedge")), issues.ToString());
        }

        [Test]
        public void AResourceWithoutAnAmount_IsAnError()
        {
            var spec = MapFixture.Spec();
            spec.resources.Add(new MapResourceSpec { kind = "Oil" });

            var issues = CheckSpec(spec, "Oil");

            Assert.IsTrue(issues.Errors.Any(e => e.Contains("Resource 1") && e.Contains("amount")), issues.ToString());
        }

        [Test]
        public void ACheckedSpec_HasNoErrors()
        {
            var spec = MapFixture.Spec();
            spec.roads.Add(MapFixture.Road("Dirt", new Vector2(-50f, -100f), new Vector2(-50f, -50f)));

            var issues = CheckSpec(spec);

            Assert.IsFalse(issues.HasErrors, issues.ToString());
        }

        [TestCase("Cobbles", 0f, "Cobbles", TestName = "AnUnknownAccessClass_IsAnErrorNamingIt")]
        [TestCase(null, 20f, "sharedAccessDistance", TestName = "ASharedAccessDistanceWithoutAccess_IsAnErrorNamingIt")]
        [TestCase("Dirt", -1f, "sharedAccessDistance", TestName = "ANegativeSharedAccessDistance_IsAnErrorNamingIt")]
        public void ABadAccessEntry_IsAnErrorNamingIt(string access, float distance, string named)
        {
            var palette = new MapPalette();
            palette.entries.Add(new MapProp { key = "Shed", access = access, sharedAccessDistance = distance });

            var issues = CheckAssets(MapFixture.Climate(), palette);

            Assert.IsTrue(issues.Errors.Any(e => e.Contains("Shed") && e.Contains(named)), issues.ToString());
        }

        [TestCase("", "'weather' key is missing")]
        [TestCase("Blizzard", "weather 'Blizzard' is not in climate")]
        public void ASpecWeatherOutsideTheClimate_IsAnErrorNamingIt(string weather, string error)
        {
            var spec = MapFixture.Spec();
            spec.weather = weather;

            var issues = CheckSpec(spec);

            Assert.IsTrue(issues.Errors.Any(e => e.Contains(error)), issues.ToString());
        }

        [Test]
        public void ClimateWeathers_NeedUniqueKeysAndAtmospheres()
        {
            var climate = MapFixture.Climate();
            climate.weathers.Add(new MapClimateWeather { key = "Clear" });

            var issues = CheckAssets(climate, new MapPalette());

            Assert.IsTrue(issues.Errors.Any(e => e.Contains("weather 'Clear' is listed twice")), issues.ToString());
            Assert.IsTrue(issues.Errors.Any(e => e.Contains("weather 'Clear' has no atmosphere")), issues.ToString());
        }

        [Test]
        public void MissingClimateValues_AreErrorsNamingThem()
        {
            var climate = MapFixture.Climate();
            climate.farmlandKits.Add(new MapFarmlandKit { key = "Farms", marginSurface = "Meadow" });

            var issues = CheckAssets(climate, new MapPalette());

            foreach (var field in new[] { "fordShallowsWidth", "fordRapidsLength", "trackJoinDistance", "groundTint regionLight" })
            {
                Assert.IsTrue(issues.Errors.Any(e => e.Contains(field)), $"{field}: {issues}");
            }
        }

        [Test]
        public void AWoodlandTreeWithMaxEdgeDistance_IsAnErrorNamingIt()
        {
            var climate = MapFixture.Climate();
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
            var climate = MapFixture.Climate();
            climate.surfaces.Single(s => s.key == "Meadow").props.Add(new MapPropDensity { kind = "Milestone" });
            var palette = new MapPalette();
            palette.entries.Add(new MapProp { key = "Milestone", besideRoad = true });

            var issues = CheckAssets(climate, palette);

            Assert.IsTrue(issues.Errors.Any(e => e.Contains("Milestone") && e.Contains("besideRoad")), issues.ToString());
        }

        [Test]
        public void ABesideRoadSpecProp_IsAnErrorNamingIt()
        {
            var spec = MapFixture.Spec();
            spec.props.Add(new MapPropSpec { kind = "Milestone" });
            var rules = new MapRules();
            rules.modes.Add(new MapModeRules { mode = spec.mode });
            var palette = new MapPalette();
            palette.entries.Add(new MapProp { key = "Milestone", besideRoad = true });
            var issues = new MapIssues();

            MapSpecCheck.Check(spec, MapFixture.Climate(), palette, rules, issues);

            Assert.IsTrue(issues.Errors.Any(e => e.Contains("Prop 1") && e.Contains("besideRoad")), issues.ToString());
        }

        [Test]
        public void PaletteSides_NeedValidUniqueIdsAndKnownKitKeys()
        {
            var palette = new MapPalette();
            palette.sides.Add(new MapPaletteSide { side = 0 });
            var side = new MapPaletteSide { side = 2 };
            side.startKit.Add(new MapStartItem { key = "Bunker" });
            palette.sides.Add(side);
            palette.sides.Add(new MapPaletteSide { side = 2, buildOrder = new MapAssetRef { path = "Assets/Missing.asset" } });

            var issues = CheckAssets(MapFixture.Climate(), palette);

            Assert.IsTrue(issues.Errors.Any(e => e.Contains("side 0") && e.Contains("1 to 255")), issues.ToString());
            Assert.IsTrue(issues.Errors.Any(e => e.Contains("side 2 is listed twice")), issues.ToString());
            Assert.IsTrue(issues.Errors.Any(e => e.Contains("'Bunker' is not in the palette")), issues.ToString());
            Assert.IsTrue(issues.Errors.Any(e => e.Contains("Assets/Missing.asset")), issues.ToString());
        }

        [Test]
        public void APlayAreaPropWithoutEntityAuthoring_IsAnErrorNamingIt()
        {
            var (stone, fence) = (new GameObject("Stone"), new GameObject("Fence"));
            fence.AddComponent<DestructibleAuthoring>();
            var issues = new MapIssues();
            var context = MapFixture.Context(MapFixture.Spec(), MapFixture.Climate(), issues);
            context.Placements.Add(new MapPlacement(new MapProp { key = "Stone", prefab = stone }, Vector2.zero, 0f, 1f));
            context.Placements.Add(new MapPlacement(new MapProp { key = "Fence", prefab = fence }, Vector2.one, 0f, 1f));

            context.Placements.CheckEntities(issues);
            Object.DestroyImmediate(stone);
            Object.DestroyImmediate(fence);

            Assert.AreEqual(1, issues.Errors.Count(), issues.ToString());
            Assert.IsTrue(issues.Errors.Single().Contains("'Stone'"), issues.ToString());
        }

        [Test]
        public void TheReport_KeepsEachIssuesFeature_ThroughJson()
        {
            var issues = new MapIssues();
            issues.Error("map-wide");
            issues.Warn("about a road", new MapIssueFeature(nameof(MapSpec.roads), 2));

            var json = JsonUtility.ToJson(MapReport.Create(MapFixture.Spec(), issues, new MapMetrics()));
            var report = JsonUtility.FromJson<MapReport>(json);

            Assert.IsTrue(report.errorFeatures.Single().IsNone);
            Assert.AreEqual(new MapIssueFeature(nameof(MapSpec.roads), 2), report.warningFeatures.Single());
            Assert.AreEqual("about a road", report.warnings.Single(), "the messages stay a plain list");
        }

        // Stacks are flattened x, z pairs.
        [TestCase(0, TestName = "NoStacksAreNoFields")]
        [TestCase(1, 0f, 0f, 8f, 6f, TestName = "ClosePairIsOneField")]
        [TestCase(2, 0f, 0f, 8f, 6f, 100f, 0f, 106f, 8f, TestName = "DistantPairsAreSeparateFields")]
        [TestCase(1, 0f, 0f, 36f, 0f, 18f, 0f, TestName = "ChainOfStacksIsOneField")]
        public void TheReport_GroupsResourceStacksIntoFields(int expected, params float[] xz)
        {
            var stacks = Enumerable.Range(0, xz.Length / 2).Select(i => new Vector2(xz[i * 2], xz[i * 2 + 1])).ToList();

            Assert.AreEqual(expected, MapFieldClusters.Count(stacks));
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
                palette.entries.Add(new MapProp { key = key });
            }

            var issues = new MapIssues();
            MapSpecCheck.Check(spec, MapFixture.Climate(), palette, rules, issues);
            return issues;
        }
    }
}
