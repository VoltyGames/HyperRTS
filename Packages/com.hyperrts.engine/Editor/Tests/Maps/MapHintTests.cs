using System;
using System.Collections.Generic;
using System.Linq;
using HyperRTS.Editor.Maps;
using HyperRTS.Editor.Maps.Hints;
using HyperRTS.Editor.Maps.Layout;
using HyperRTS.Editor.Maps.Nav;
using HyperRTS.Simulation.AI;
using HyperRTS.Simulation.Navigation;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;

namespace HyperRTS.Editor.Tests.Maps
{
    /// <summary>
    /// The bot hints a map builder computes, on small synthetic nav grids: a 200 by 80 m field (1 m cells) whose
    /// middle wall at x = 0 has two 4 m gaps at z = 20 and z = -20, or no wall at all.
    /// </summary>
    public class MapHintTests
    {
        private const float BaseRadius = 8f;
        private const float CorridorWidth = 6f;

        private MapNavGrid _nav;
        private AIMapHintsAuthoring _hints;
        private MapIssues _issues;

        [SetUp]
        public void SetUp()
        {
            _hints = new GameObject("Hints").AddComponent<AIMapHintsAuthoring>();
            _issues = new MapIssues();
        }

        [TearDown]
        public void TearDown()
        {
            _nav?.Dispose();
            UnityEngine.Object.DestroyImmediate(_hints.gameObject);
        }

        private static bool Wall(float2 cell)
        {
            var inWall = math.abs(cell.x) < 1f;
            var inGap = math.abs(cell.y) > 18f && math.abs(cell.y) < 22f;
            return inWall && !inGap;
        }

        private static MapNavGrid Field(Func<float2, bool> blocked)
        {
            var size = new int2(200, 80);
            var min = new float2(-100f, -40f);
            var grid = NavGrid.Create(size, min, 1f, default, 0f);
            for (var i = 0; i < grid.Cells.Length; i++)
            {
                var center = min + new float2(i % size.x, i / size.x) + 0.5f;
                grid.Cells[i] = (byte)(blocked(center) ? NavSurface.Blocked : NavSurface.Land);
            }

            return MapNavGrid.Wrap(grid);
        }

        private static MapSpawn Spawn(int slot, int team, float x, float z) => new(new Vector2(x, z), 0f, slot, team, 0);

        private bool Fill(IReadOnlyList<MapSpawn> spawns, List<(Vector2, int)> nodes = null,
            List<(Vector2, float)> crossings = null)
        {
            var input = new MapHintInput(_nav, spawns, nodes ?? new List<(Vector2, int)>(),
                crossings ?? new List<(Vector2, float)>(), BaseRadius, CorridorWidth);
            return MapHintBuilder.Fill(_hints, input, _issues);
        }

        private List<AIMapHintsAuthoring.LaneHints> Lanes(int from, int to) =>
            _hints.lanes.Where(l => l.from == from && l.to == to).ToList();

        private AIMapHintsAuthoring.SlotHints Slot(int slot) => _hints.slots.Single(s => s.slot == slot);

        private List<MapSpawn> Duel() => new() { Spawn(1, 1, -60f, 0f), Spawn(2, 2, 60f, 0f) };

        /// <summary>The duel with one resource field beside each base.</summary>
        private bool FillDuel() =>
            Fill(Duel(), new List<(Vector2, int)> { (new Vector2(-55f, 5f), 1), (new Vector2(55f, -5f), 2) });

        private MapHintInput Input(List<(Vector2, float)> crossings) => new(_nav, Duel(),
            new List<(Vector2, int)>(), crossings, BaseRadius, CorridorWidth);

        [Test]
        public void Duel_HasLanesInBothDirections_OnePerGap()
        {
            _nav = Field(Wall);

            Assert.IsTrue(FillDuel());

            Assert.IsFalse(_issues.HasErrors, _issues.ToString());
            var there = Lanes(1, 2);
            var back = Lanes(2, 1);
            Assert.AreEqual(2, there.Count, "one lane through each gap");
            Assert.AreEqual(2, back.Count);
            Assert.AreNotEqual(Math.Sign(there[0].waypoints[0].y), Math.Sign(there[1].waypoints[0].y));
            for (var k = 0; k < there.Count; k++)
            {
                CollectionAssert.AreEqual(there[k].waypoints.AsEnumerable().Reverse(), back[k].waypoints,
                    "lane k is the same path both ways");
            }
        }

        [Test]
        public void Lanes_AreWaypointsAboutFiftyMetresApart_NeitherBaseIncluded()
        {
            _nav = Field(Wall);

            FillDuel();

            foreach (var lane in Lanes(1, 2))
            {
                var path = new[] { new Vector2(-60f, 0f) }.Concat(lane.waypoints).Append(new Vector2(60f, 0f)).ToList();
                Assert.GreaterOrEqual(lane.waypoints.Count, 2);
                for (var i = 1; i < path.Count - 1; i++)
                {
                    var gap = Vector2.Distance(path[i - 1], path[i]);
                    Assert.That(gap, Is.InRange(35f, 55f), $"waypoint {i}");
                }
            }
        }

        [Test]
        public void Expansions_AreOrderedByWalkingDistance_TheSlotsOwnFieldFirst()
        {
            _nav = Field(Wall);
            var own1 = (new Vector2(-55f, 5f), 1);
            var west = (new Vector2(-20f, -10f), 0);
            var farWest = (new Vector2(-2f, 38f), 0);
            var behindWall = (new Vector2(3f, 0f), 0);
            var own2 = (new Vector2(55f, -5f), 2);

            Fill(Duel(), new List<(Vector2, int)> { behindWall, own2, farWest, west, own1 });

            Assert.IsFalse(_issues.HasErrors, _issues.ToString());
            CollectionAssert.AreEqual(new[] { own1.Item1, west.Item1, farWest.Item1, behindWall.Item1, own2.Item1 },
                Slot(1).expansions, "the field behind the wall is nearer as the crow flies but farther to walk");
            Assert.AreEqual(own2.Item1, Slot(2).expansions[0]);
            Assert.AreEqual(own1.Item1, Slot(2).expansions.Last());
        }

        [Test]
        public void Defence_SitsWhereTheLanesPassTheNarrowGaps()
        {
            _nav = Field(Wall);

            FillDuel();

            foreach (var slot in new[] { 1, 2 })
            {
                var defence = Slot(slot).defence;
                Assert.AreEqual(2, defence.Count, $"slot {slot}: a point per gap");
                foreach (var point in defence)
                {
                    var toGap = Mathf.Min(Vector2.Distance(point, new Vector2(0f, 20f)),
                        Vector2.Distance(point, new Vector2(0f, -20f)));
                    Assert.Less(toGap, 6f, $"slot {slot}: {point}");
                }
            }
        }

        [Test]
        public void Defence_StopsAtACrossingOnTheLane_ElseWhereTheLaneLeavesTheBase()
        {
            _nav = Field(_ => false);
            var cells = Enumerable.Range(40, 121).Select(x => new int2(x, 40)).ToList();
            var west = new MapHintSlot(Spawn(1, 1, -60f, 0.5f), new int2(40, 40), 40 * 200 + 40, Array.Empty<float>());
            var east = new MapHintSlot(Spawn(2, 2, 60f, 0.5f), new int2(160, 40), 40 * 200 + 160, Array.Empty<float>());
            var lane = new MapHintLane(west, east, new MapPath(cells, 120f, 0), new List<Vector2>());
            var crossings = new List<(Vector2, float)> { (new Vector2(30f, 0.5f), 5f) };

            var withCrossing = MapHintDefence.Find(Input(crossings), east, new[] { lane });
            var without = MapHintDefence.Find(Input(new List<(Vector2, float)>()), east, new[] { lane });

            Assert.AreEqual(1, withCrossing.Count);
            Assert.Less(Vector2.Distance(withCrossing[0], new Vector2(35f, 0.5f)), 1.5f, "the crossing's near edge");
            Assert.AreEqual(1, without.Count);
            Assert.Less(Vector2.Distance(without[0], new Vector2(51.5f, 0.5f)), 1.5f, "just outside the base");
        }

        [Test]
        public void Allies_GetNoLaneBetweenThem_AndEveryEnemyPairGetsOne()
        {
            _nav = Field(_ => false);
            var spawns = new List<MapSpawn>
            {
                Spawn(1, 1, -70f, -20f), Spawn(2, 2, 70f, -20f), Spawn(3, 1, -70f, 20f), Spawn(4, 2, 70f, 20f),
            };
            var nodes = spawns.Select(s => (s.Position + Vector2.right * 5f, s.Slot)).ToList();

            Fill(spawns, nodes);

            Assert.IsFalse(_issues.HasErrors, _issues.ToString());
            Assert.IsEmpty(Lanes(1, 3));
            Assert.IsEmpty(Lanes(2, 4));
            foreach (var (from, to) in new[] { (1, 2), (1, 4), (3, 2), (3, 4) })
            {
                Assert.IsNotEmpty(Lanes(from, to), $"{from} to {to}");
                Assert.IsNotEmpty(Lanes(to, from), $"{to} to {from}");
            }
        }

        [Test]
        public void ABaseOnBlockedGround_FailsLoudly()
        {
            _nav = Field(_ => true);

            Assert.IsFalse(Fill(Duel()));

            Assert.IsTrue(_issues.Errors.Any(e => e.Message.Contains("slot 1")), _issues.ToString());
            Assert.IsEmpty(_hints.lanes);
        }

        [Test]
        public void ASlotWithNoReachableResourceNode_IsAnError()
        {
            _nav = Field(Wall);

            Fill(Duel(), new List<(Vector2, int)>());

            Assert.IsTrue(_issues.Errors.Any(e => e.Message.Contains("slot 1") && e.Message.Contains("resource")), _issues.ToString());
        }

        [Test]
        public void TheAuthoringBakesIntoValidData()
        {
            _nav = Field(Wall);
            FillDuel();

            var data = _hints.ToData();

            Assert.AreEqual(2, data.Slots.Count);
            Assert.AreEqual(4, data.Lanes.Count);
            Assert.AreEqual(2, data.Slots[0].ExpansionCount, "every reachable field is in every slot's order");
            Assert.AreEqual(_hints.lanes[0].waypoints.Count, data.Lanes[0].Count);
        }
    }
}
