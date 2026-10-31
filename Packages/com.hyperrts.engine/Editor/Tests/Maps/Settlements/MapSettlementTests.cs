using System;
using System.Collections.Generic;
using System.Linq;
using HyperRTS.Editor.Maps.Geometry;
using HyperRTS.Editor.Maps.Land;
using HyperRTS.Editor.Maps.Palette;
using HyperRTS.Editor.Maps.Placement;
using HyperRTS.Editor.Maps.Planning.Settlements;
using NUnit.Framework;
using Unity.Scripting.LifecycleManagement;
using UnityEngine;

namespace HyperRTS.Editor.Tests.Maps.Settlements
{
    /// <summary>Settlements on a straight road: plots, bases and water, copies, counts, kit checks and street pipe runs.</summary>
    public class MapSettlementTests
    {
        private const float RoadZ = MapSettlementFixture.RoadZ;

        [NoAutoStaticsCleanup]
        private static readonly TestCaseData[] BrokenKits =
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

        private MapSettlementFixture _fixture;

        [TearDown]
        public void TearDown()
        {
            _fixture?.Dispose();
            _fixture = null;
        }

        [Test]
        public void Plots_LineBothSidesOfAStraightRoad_WithHousesFacingTheStreet()
        {
            _fixture = MapSettlementFixture.Plan();

            var houses = _fixture.InSector("House");
            Assert.IsFalse(_fixture.Context.Issues.HasErrors, _fixture.Context.Issues.ToString());
            Assert.AreEqual(6, houses.Count, "the asked count");
            Assert.IsFalse(Warned("plots"), "no warning when every plot fits");
            Assert.IsTrue(houses.Any(h => h.Position.y > RoadZ), "a plot north of the road");
            Assert.IsTrue(houses.Any(h => h.Position.y < RoadZ), "a plot south of the road");
            foreach (var house in houses)
            {
                var toRoad = new Vector2(0f, RoadZ - house.Position.y).normalized;
                Assert.Greater(Vector2.Dot(MapGeometry.Forward(house.Yaw), toRoad), 0.99f, "faces the road");
                Assert.Greater(Mathf.Abs(house.Position.y - RoadZ), 12f, "set back from the road");
            }
        }

        [Test]
        public void Plots_StayOutOfBases()
        {
            _fixture = MapSettlementFixture.Plan(spawn: new Vector2(0f, RoadZ), count: 50);

            var context = _fixture.Context;
            var built = _fixture.Placements.Where(p => p.Entry.key == "House" || p.Entry.key == "Barn").ToList();
            Assert.Greater(built.Count, 0);
            foreach (var placement in built)
            {
                foreach (var spawn in context.Layout.Spawns)
                {
                    var distance = (placement.Position - spawn.Position).magnitude;
                    Assert.Greater(distance, MapSettlementFixture.BaseRadius - 4f, $"{placement.Entry.key} in a base");
                }
            }

            Assert.IsTrue(Warned("in a base"), "the warning names the base");
        }

        [Test]
        public void Plots_StayOffWater()
        {
            _fixture = MapSettlementFixture.Plan(river: true, count: 50);

            var context = _fixture.Context;
            var houses = _fixture.InSector("House");
            Assert.Greater(houses.Count, 0, "the dry side still gets plots");
            Assert.IsTrue(houses.All(h => h.Position.y > RoadZ), "the river side has none");
            foreach (var placement in _fixture.Placements)
            {
                Assert.Greater(context.Shapes.RiverEdgeDistance(placement.Position), 0f, $"{placement.Entry.key} on water");
            }

            Assert.IsTrue(Warned("touching water"), "the warning names the water");
        }

        [Test]
        public void Copies_MatchUnderRotate180()
        {
            _fixture = MapSettlementFixture.Plan();

            var placements = _fixture.Placements;
            var sector = placements.Where(p => p.Position.y <= 0f).ToList();
            Assert.Greater(sector.Count, 20);
            Assert.AreEqual(sector.Count * 2, placements.Count, "every sector placement has one copy");
            foreach (var placement in sector)
            {
                Assert.IsTrue(placements.Any(other => IsRotatedCopy(placement, other)),
                    $"{placement.Entry.key} at {placement.Position} has a rotated copy");
            }

            AssertLandMatches(_fixture.Context.Land);
        }

        [Test]
        public void WarnsWhenFewerPlotsFitThanAsked_AndSaysWhy()
        {
            _fixture = MapSettlementFixture.Plan(count: 40);

            var houses = _fixture.InSector("House");
            Assert.Less(houses.Count, 40);
            Assert.Greater(houses.Count, 0);
            Assert.IsTrue(Warned($"fit {houses.Count} of 40 plots"), string.Join("\n", _fixture.Context.Issues.Warnings));
            Assert.IsTrue(Warned("rejected"), "the warning lists the reasons");
        }

        [Test]
        public void WarnsWhenThereIsNoRoad()
        {
            _fixture = MapSettlementFixture.Plan(road: false);

            Assert.AreEqual(0, _fixture.Placements.Count(p => p.Entry.key == "House"));
            Assert.IsTrue(Warned("no road within"));
            Assert.IsFalse(_fixture.Context.Issues.HasErrors);
        }

        [Test]
        public void ClaimsYardGardenAndBack_AndPlantsTheOrchardAtTheBack()
        {
            _fixture = MapSettlementFixture.Plan();

            var context = _fixture.Context;
            var houses = _fixture.InSector("House");
            Assert.IsTrue(houses.All(h => context.Land.KindAt(h.Position) == MapLandKind.Yard), "houses stand in the yard");
            var claimed = new HashSet<MapLandKind>();
            context.Land.ForEach((_, _, center) => claimed.Add(context.Land.KindAt(center)));
            CollectionAssert.IsSupersetOf(claimed, new[] { MapLandKind.Garden, MapLandKind.Back });
            var orchard = context.Trees.Planted.Where(t => t.Position.y <= 0f).ToList();
            Assert.Greater(orchard.Count, 0, "the orchard is planted");
            Assert.IsTrue(orchard.All(t => context.Land.KindAt(t.Position) == MapLandKind.Back), "trees grow on the back land");
        }

        [Test]
        public void FurnishesPlots_FencesGatesOutbuildingsAndExtras()
        {
            _fixture = MapSettlementFixture.Plan();

            var houses = _fixture.Placements.Count(p => p.Entry.key == "House");
            Assert.AreEqual(houses, _fixture.Placements.Count(p => p.Entry.key == "Gate"), "one gate per plot");
            Assert.Greater(_fixture.Placements.Count(p => p.Entry.key == "Fence"), houses * 8, "fences along the plots");
            Assert.LessOrEqual(_fixture.Placements.Count(p => p.Entry.key == "Barn"), houses, "at most one outbuilding per plot");
            Assert.Greater(_fixture.InSector("Well").Count, 0, "a well with chance 1 is placed");
            foreach (var house in _fixture.Placements.Where(p => p.Entry.key == "House"))
            {
                Assert.IsFalse(_fixture.Context.Placements.Clearance.IsFree(house.Position, 0.5f), "the house reserves its ground");
            }
        }

        [Test]
        public void StreetPolesAndPipe_RunOnOppositeSidesOfTheRoad()
        {
            _fixture = MapSettlementFixture.Plan();

            var poles = _fixture.InSector("Pole");
            var pipes = _fixture.InSector("Pipe");
            Assert.Greater(poles.Count, 0);
            Assert.Greater(pipes.Count, 0);
            Assert.IsTrue(poles.All(p => p.Position.y > RoadZ), "poles on one side");
            Assert.IsTrue(pipes.All(p => p.Position.y < RoadZ), "pipe on the other");
            Assert.IsTrue(poles.All(p => Mathf.Abs(p.Position.y - RoadZ) < 6f), "poles stand by the road edge");
        }

        [Test]
        public void EmptyKitFields_MeanTheKitHasNone()
        {
            _fixture = MapSettlementFixture.Plan(edit: kit =>
            {
                kit.frontFence = kit.sideFence = kit.gate = kit.streetPipe = kit.streetPole = kit.orchardTree = string.Empty;
                kit.outbuildings.Clear();
                kit.extras.Clear();
            });

            Assert.IsFalse(_fixture.Context.Issues.HasErrors, _fixture.Context.Issues.ToString());
            Assert.AreEqual(6, _fixture.InSector("House").Count);
            Assert.AreEqual(_fixture.Placements.Count(p => p.Entry.key == "House"), _fixture.Placements.Count, "only houses");
            Assert.AreEqual(0, _fixture.Context.Trees.Planted.Count);
        }

        [TestCaseSource(nameof(BrokenKits))]
        public void ABrokenKit_IsAnErrorNamingTheKit(Action<MapSettlementKit> edit, string named)
        {
            var issues = MapSettlementFixture.CheckAssets(edit);

            Assert.IsTrue(issues.Errors.Any(e => e.Message.Contains("Village") && e.Message.Contains(named)), issues.ToString());
        }

        [Test]
        public void PipeRuns_RunsShorterThanMinSections_AreLeftOut()
        {
            var arches = new List<(int First, int Count)>();

            var kept = MapPipeRuns.SectionsToLay(Spots("##....#....####"), 3, 1, arches);

            Assert.AreEqual("...........####", Pattern(kept));
            Assert.IsEmpty(arches);
        }

        [Test]
        public void PipeRuns_AShortGapOverARoad_IsBridged_AndJoinsTheRunsEitherSide()
        {
            var arches = new List<(int First, int Count)>();

            var kept = MapPipeRuns.SectionsToLay(Spots("##..##......#"), 3, 2, arches);

            Assert.AreEqual("##..##.......", Pattern(kept), "two runs of 2 across the gap make 4; the lone section goes");
            Assert.AreEqual(new List<(int, int)> { (2, 2) }, arches);
        }

        [Test]
        public void PipeRuns_AGapWiderThanAnArchSpans_SplitsTheRuns()
        {
            var arches = new List<(int First, int Count)>();

            var kept = MapPipeRuns.SectionsToLay(Spots("###...###"), 3, 2, arches);

            Assert.AreEqual("###...###", Pattern(kept));
            Assert.IsEmpty(arches);
        }

        [Test]
        public void PipeRuns_AnArchOverATwoSectionGap_IsStretched_SoItsEndsMeetTheRuns()
        {
            const float section = 6f;
            const float archLength = 4.6f;
            var arches = new List<(int First, int Count)>();
            MapPipeRuns.SectionsToLay(Spots("###..###"), 3, 2, arches);
            var (first, count) = arches.Single();

            var half = archLength * MapPipeRuns.ArchStretch(count, section, archLength) * 0.5f;
            var middle = (first + count * 0.5f) * section;

            Assert.AreEqual(2, count);
            Assert.AreEqual(first * section, middle - half, 1e-4f, "meets the run before the gap");
            Assert.AreEqual((first + count) * section, middle + half, 1e-4f, "meets the run after it");
        }

        private static bool[] Spots(string pattern) => pattern.Select(c => c == '#').ToArray();

        private static string Pattern(bool[] kept) => new(kept.Select(k => k ? '#' : '.').ToArray());

        private bool Warned(string text) => _fixture.Context.Issues.Warnings.Any(w => w.Message.Contains(text));

        private static bool IsRotatedCopy(MapPlacement placement, MapPlacement other)
        {
            if (other.Entry != placement.Entry)
            {
                return false;
            }

            if ((other.Position + placement.Position).magnitude > 0.01f)
            {
                return false;
            }

            var turned = Mathf.DeltaAngle(other.Yaw, placement.Yaw + 180f);
            return Mathf.Abs(turned) < 0.01f && Mathf.Approximately(other.Scale, placement.Scale);
        }

        private static void AssertLandMatches(MapLand land)
        {
            var plotCells = 0;
            land.ForEach((x, z, center) =>
            {
                var kind = land.KindAt(center);
                if (kind != MapLandKind.Yard && kind != MapLandKind.Garden && kind != MapLandKind.Back)
                {
                    return;
                }

                plotCells++;
                Assert.AreEqual(kind, land.KindAt(-center), $"the copy of ({center.x}, {center.y}) is the same use");
            });
            Assert.Greater(plotCells, 1000);
        }
    }
}
