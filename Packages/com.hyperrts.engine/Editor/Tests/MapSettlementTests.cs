using System.Collections.Generic;
using System.Linq;
using HyperRTS.Editor.Maps.Geometry;
using HyperRTS.Editor.Maps.Land;
using HyperRTS.Editor.Maps.Placement;
using NUnit.Framework;
using UnityEngine;

namespace HyperRTS.Editor.Tests
{
    /// <summary>Settlement planning on a straight road: both sides, bases and water, copies, counts and warnings.</summary>
    public class MapSettlementTests
    {
        private const float RoadZ = MapSettlementFixture.RoadZ;
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
            Assert.AreEqual(6, houses.Count);
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

        [TestCase(1)]
        [TestCase(3)]
        public void Count_IsRespected(int count)
        {
            _fixture = MapSettlementFixture.Plan(count: count);

            Assert.AreEqual(count, _fixture.InSector("House").Count);
            Assert.AreEqual(count * 2, _fixture.Placements.Count(p => p.Entry.key == "House"), "one per copy");
            Assert.IsFalse(Warned("plots"), "no warning when every plot fits");
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
            var claimed = KindsClaimed(context.Land, MapLandKind.Garden, MapLandKind.Back).ToList();
            CollectionAssert.Contains(claimed, MapLandKind.Garden);
            CollectionAssert.Contains(claimed, MapLandKind.Back);
            var orchard = context.Trees.Where(t => t.Position.y <= 0f).ToList();
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
                Assert.IsFalse(_fixture.Context.Placer.Clearance.IsFree(house.Position, 0.5f), "the house reserves its ground");
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
            Assert.AreEqual(0, _fixture.Context.Trees.Count);
        }

        [Test]
        public void AKeyThatDoesNotResolve_IsAnErrorNamingTheKitAndKey()
        {
            _fixture = MapSettlementFixture.Plan(edit: kit => kit.houses[0] = "Nope");

            var errors = _fixture.Context.Issues.Errors;
            Assert.IsTrue(errors.Any(e => e.Contains("Village") && e.Contains("Nope")), string.Join("\n", errors));
            Assert.AreEqual(0, _fixture.Placements.Count(p => p.Entry.key == "House"));
        }

        [Test]
        public void AnUnknownSurface_IsAnErrorNamingTheKitAndSurface()
        {
            _fixture = MapSettlementFixture.Plan(edit: kit => kit.gardenSurface = "Mud");

            var errors = _fixture.Context.Issues.Errors;
            Assert.IsTrue(errors.Any(e => e.Contains("Village") && e.Contains("Mud")), string.Join("\n", errors));
        }

        private bool Warned(string text) => _fixture.Context.Issues.Warnings.Any(w => w.Contains(text));

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

        private static IEnumerable<MapLandKind> KindsClaimed(MapLand land, params MapLandKind[] kinds)
        {
            var found = new HashSet<MapLandKind>();
            land.ForEach((x, z, center) =>
            {
                var kind = land.KindAt(center);
                if (kinds.Contains(kind))
                {
                    found.Add(kind);
                }
            });
            return found;
        }
    }
}
