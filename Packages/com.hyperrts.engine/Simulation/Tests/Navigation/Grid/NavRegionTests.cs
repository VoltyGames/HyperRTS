using HyperRTS.Simulation.Common.Authoring;
using System.Collections.Generic;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Destruction;
using HyperRTS.Simulation.Navigation.Agents;
using HyperRTS.Simulation.Navigation.Areas;
using HyperRTS.Simulation.Navigation.Fields;
using HyperRTS.Simulation.Navigation.Grid;
using HyperRTS.Simulation.Navigation.Sectors;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Tests.Navigation.Grid
{
    /// <summary>
    /// The nav grid's sector regions: they match a flood fill and, rebuilt piece by piece, a whole build.
    /// </summary>
    public class NavRegionTests : SimulationFixture
    {
        private static readonly NavAgent Infantry = new() { Radius = 0.5f, SceneryCrushLimit = NavAccess.AllScenery };
        private static readonly NavAgent Tank = new() { Radius = 1.85f, SceneryCrushLimit = 3 };
        private static readonly NavAgent Boat = new() { Radius = 1f, Layer = NavLayer.Naval };

        [SetUp]
        public void Prepare()
        {
            World.AddAgentPrefabs(Infantry, Tank, Boat);
        }

        // Rotated buildings, some closing pockets.
        private List<Entity> SpawnClutter(uint seed, int count)
        {
            var random = new Random(seed);
            var placed = new List<Entity>();
            for (var i = 0; i < count; i++)
            {
                var center = new float3(random.NextFloat(-95f, 95f), 0f, random.NextFloat(-95f, 95f));
                var size = random.NextFloat2(new float2(2f), new float2(18f));
                placed.Add(World.SpawnObstacle(center, size, random.NextFloat(math.PI)));
            }

            return placed;
        }

        private Entity PlantTree(float3 position, int crush)
        {
            var tree = World.EntityManager.CreateEntity();
            World.Add(tree, LocalTransform.FromPosition(position));
            var writer = World.Writer(tree);
            DestructibleAuthoring.AddScenery(writer, 50f, crush, Entity.Null, float2.zero,
                SceneryId.At(position, quaternion.identity));
            return tree;
        }

        [Test]
        public void Regions_MatchAFloodFillOverOpenCells()
        {
            World.SpawnNavArea(new float3(0f, 0f, 30f), new float2(200f, 12f), NavAreaKind.Water);
            World.SpawnNavArea(new float3(-40f, 0f, 30f), new float2(6f, 14f), NavAreaKind.Deck);
            // A closed ring, so its inside is a region of its own.
            World.SpawnObstacle(new float3(50f, 0f, -50f), new float2(20f, 2f));
            World.SpawnObstacle(new float3(50f, 0f, -70f), new float2(20f, 2f));
            World.SpawnObstacle(new float3(41f, 0f, -60f), new float2(2f, 20f));
            World.SpawnObstacle(new float3(59f, 0f, -60f), new float2(2f, 20f));
            SpawnClutter(11, 30);
            World.Tick();

            var grid = World.Grid();
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
                var region = access.Region(grid.Grid.Cell(i));
                Assert.AreEqual(flood[i] == 0, region == 0, $"{what}: cell {grid.Grid.Cell(i)} open");
                if (flood[i] == 0)
                {
                    continue;
                }

                if (toRegion.TryGetValue(flood[i], out var known))
                {
                    Assert.AreEqual(known, region, $"{what}: one flood area, one region, at {grid.Grid.Cell(i)}");
                }

                if (toFlood.TryGetValue(region, out var area))
                {
                    Assert.AreEqual(area, flood[i], $"{what}: one region, one flood area, at {grid.Grid.Cell(i)}");
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
                if (labels[i] != 0 || !access.IsOpen(grid.Grid.Cell(i)))
                {
                    continue;
                }

                labels[i] = ++label;
                stack.Push(grid.Grid.Cell(i));
                while (stack.Count > 0)
                {
                    var cell = stack.Pop();
                    foreach (var step in new[] { new int2(1, 0), new int2(-1, 0), new int2(0, 1), new int2(0, -1) })
                    {
                        var next = cell + step;
                        if (access.IsOpen(next) && labels[grid.Grid.Index(next)] == 0)
                        {
                            labels[grid.Grid.Index(next)] = label;
                            stack.Push(next);
                        }
                    }
                }
            }

            return labels;
        }

        [Test]
        public void Rebuilds_AfterBuildingsAndSceneryChange_MatchAWholeBuild()
        {
            World.SpawnNavArea(new float3(40f, 0f, 0f), new float2(20f, 200f), NavAreaKind.Water);
            var placed = SpawnClutter(5, 10);
            World.Tick();
            var random = new Random(9);
            for (var round = 0; round < 6; round++)
            {
                placed.AddRange(SpawnClutter(random.NextUInt(1, uint.MaxValue), 3));
                for (var i = 0; i < 3; i++)
                {
                    var spot = new float3(random.NextFloat(-90f, 90f), 0f, random.NextFloat(-90f, 90f));
                    placed.Add(PlantTree(spot, random.NextInt(1, 6)));
                }

                World.EntityManager.DestroyEntity(placed[random.NextInt(placed.Count)]);
                World.Tick();
                AssertMatchesWholeBuild($"round {round}");
            }
        }

        private void AssertMatchesWholeBuild(string when)
        {
            var grid = World.Grid();
            var whole = NavGrid.Create(grid.Grid, grid.Terrain, grid.WaterLevel);
            try
            {
                whole.Cells.CopyFrom(grid.Cells);
                whole.Scenery.CopyFrom(grid.Scenery);
                foreach (var movementClass in grid.Classes.Classes)
                {
                    whole.Classes.Register(movementClass);
                }

                whole.BuildAccess();
                AssertSameField(whole.Field, grid.Field, when);
                AssertSameSectors(whole.Sectors, grid.Sectors, when);
            }
            finally
            {
                whole.Dispose();
            }
        }

        private static void AssertSameField(in NavField built, in NavField kept, string when)
        {
            Assert.AreEqual(built.Distances.Length, kept.Distances.Length, when);
            for (var i = 0; i < built.Distances.Length; i++)
            {
                if (built.Distances[i] != kept.Distances[i])
                {
                    Assert.Fail($"{when}: value {i} is {kept.Distances[i]}, not {built.Distances[i]}");
                }
            }
        }

        private static void AssertSameSectors(in NavSectors built, in NavSectors kept, string when)
        {
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
    }
}
