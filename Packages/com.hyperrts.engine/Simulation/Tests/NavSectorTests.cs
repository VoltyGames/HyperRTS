using System.Collections.Generic;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Navigation;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Tests
{
    /// <summary>
    /// The sector graph: its regions match a flood fill over the open cells, and relabelling only the dirty sectors
    /// after buildings come and go gives the same graph as a whole build.
    /// </summary>
    public class NavSectorTests
    {
        private static readonly NavAgent Infantry = new() { Radius = 0.5f, SceneryCrushLimit = NavAccess.AllScenery };
        private static readonly NavAgent Tank = new() { Radius = 1.85f, SceneryCrushLimit = 3 };
        private static readonly NavAgent Boat = new() { Radius = 1f, Layer = NavLayer.Naval };

        private TestWorld _world;

        [SetUp]
        public void SetUp()
        {
            _world = new TestWorld();
            _world.CreateMatch(1, 2);
            _world.AddAgentPrefabs(Infantry, Tank, Boat);
        }

        [TearDown]
        public void TearDown() => _world.Dispose();

        // Rotated buildings, some closing pockets, and a river with a bridge.
        private List<Entity> SpawnClutter(uint seed, int count)
        {
            var random = new Random(seed);
            var placed = new List<Entity>();
            for (var i = 0; i < count; i++)
            {
                var center = new float3(random.NextFloat(-95f, 95f), 0f, random.NextFloat(-95f, 95f));
                var size = random.NextFloat2(new float2(2f), new float2(18f));
                placed.Add(_world.SpawnObstacle(center, size, random.NextFloat(math.PI)));
            }

            return placed;
        }

        [Test]
        public void Regions_MatchAFloodFillOverOpenCells()
        {
            _world.SpawnNavArea(new float3(0f, 0f, 30f), new float2(200f, 12f), NavAreaKind.Water);
            _world.SpawnNavArea(new float3(-40f, 0f, 30f), new float2(6f, 14f), NavAreaKind.Deck);
            // A closed ring, so its inside is a region of its own.
            _world.SpawnObstacle(new float3(50f, 0f, -50f), new float2(20f, 2f), 0f);
            _world.SpawnObstacle(new float3(50f, 0f, -70f), new float2(20f, 2f), 0f);
            _world.SpawnObstacle(new float3(41f, 0f, -60f), new float2(2f, 20f), 0f);
            _world.SpawnObstacle(new float3(59f, 0f, -60f), new float2(2f, 20f), 0f);
            SpawnClutter(11, 30);
            _world.Tick();

            var grid = _world.Grid();
            foreach (var agent in new[] { Infantry, Tank, Boat })
            {
                AssertMatchesFlood(agent.Access(grid), $"radius {agent.Radius}");
            }

            Assert.Greater(AssertMatchesFlood(Tank.Access(grid), "tank"), 2, "the river and the ring split the ground");
        }

        // Each flood label maps to one region id and back; returns how many regions there are.
        private static int AssertMatchesFlood(in NavAccess access, string what)
        {
            var grid = access.Grid;
            var flood = Flood(access);
            var toRegion = new Dictionary<int, int>();
            var toFlood = new Dictionary<int, int>();
            for (var i = 0; i < flood.Length; i++)
            {
                var region = access.Region(grid.Cell(i));
                Assert.AreEqual(flood[i] == 0, region == 0, $"{what}: cell {grid.Cell(i)} open");
                if (flood[i] == 0)
                {
                    continue;
                }

                if (toRegion.TryGetValue(flood[i], out var known))
                {
                    Assert.AreEqual(known, region, $"{what}: one flood area, one region, at {grid.Cell(i)}");
                }

                if (toFlood.TryGetValue(region, out var area))
                {
                    Assert.AreEqual(area, flood[i], $"{what}: one region, one flood area, at {grid.Cell(i)}");
                }

                toRegion[flood[i]] = region;
                toFlood[region] = flood[i];
            }

            return toRegion.Count;
        }

        private static int[] Flood(in NavAccess access)
        {
            var grid = access.Grid;
            var labels = new int[grid.Cells.Length];
            var stack = new Stack<int2>();
            var label = 0;
            for (var i = 0; i < labels.Length; i++)
            {
                if (labels[i] != 0 || !access.IsOpen(grid.Cell(i)))
                {
                    continue;
                }

                labels[i] = ++label;
                stack.Push(grid.Cell(i));
                while (stack.Count > 0)
                {
                    var cell = stack.Pop();
                    foreach (var step in new[] { new int2(1, 0), new int2(-1, 0), new int2(0, 1), new int2(0, -1) })
                    {
                        var next = cell + step;
                        if (access.IsOpen(next) && labels[grid.Index(next)] == 0)
                        {
                            labels[grid.Index(next)] = label;
                            stack.Push(next);
                        }
                    }
                }
            }

            return labels;
        }

        [Test]
        public void Relabel_AfterPlacingAndRemovingBuildings_MatchesAWholeBuild()
        {
            _world.SpawnNavArea(new float3(0f, 0f, -20f), new float2(200f, 10f), NavAreaKind.Water);
            var placed = SpawnClutter(5, 10);
            _world.Tick();
            var random = new Random(9);
            for (var round = 0; round < 6; round++)
            {
                placed.AddRange(SpawnClutter(random.NextUInt(), 3));
                _world.EntityManager.DestroyEntity(placed[random.NextInt(placed.Count)]);
                _world.Tick();
                AssertMatchesWholeBuild($"round {round}");
            }
        }

        private void AssertMatchesWholeBuild(string when)
        {
            var grid = _world.Grid();
            var whole = NavGrid.Create(grid.Size, grid.Min, grid.CellSize, grid.Terrain, grid.WaterLevel);
            try
            {
                whole.Cells.CopyFrom(grid.Cells);
                whole.Scenery.CopyFrom(grid.Scenery);
                foreach (var movementClass in grid.Classes.Classes)
                {
                    whole.Classes.Register(movementClass);
                }

                whole.BuildAccess();
                var built = whole.Sectors;
                var kept = grid.Sectors;
                CollectionAssert.AreEqual(built.Labels.ToArray(), kept.Labels.ToArray(), $"{when}: labels");
                CollectionAssert.AreEqual(built.Counts.ToArray(), kept.Counts.ToArray(), $"{when}: counts");
                CollectionAssert.AreEqual(built.EdgeCounts.ToArray(), kept.EdgeCounts.ToArray(), $"{when}: edges");
                CollectionAssert.AreEqual(built.Regions.AsArray().ToArray(), kept.Regions.AsArray().ToArray(),
                    $"{when}: regions");
                for (var c = 0; c < built.ClassCount * built.SectorCount; c++)
                {
                    for (var k = 0; k < built.Counts[c]; k++)
                    {
                        var index = c * NavSectors.MaxComponents + k;
                        Assert.AreEqual(built.Representatives[index], kept.Representatives[index], $"{when}: centre");
                    }
                }
            }
            finally
            {
                whole.Dispose();
            }
        }
    }
}
