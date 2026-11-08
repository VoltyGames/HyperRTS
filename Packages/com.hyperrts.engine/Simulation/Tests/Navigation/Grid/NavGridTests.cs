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
    /// The nav grid: obstacle boxes, rebuilds when buildings change, the signed distance field, and sector regions
    /// that match a flood fill and, rebuilt piece by piece, a whole build.
    /// </summary>
    public class NavGridTests : SimulationFixture
    {
        private static readonly NavAgent Infantry = new() { Radius = 0.5f, SceneryCrushLimit = NavAccess.AllScenery };
        private static readonly NavAgent Truck = new() { Radius = 1.2f };
        private static readonly NavAgent Tank = new() { Radius = 1.85f, SceneryCrushLimit = 3 };
        private static readonly NavAgent Boat = new() { Radius = 1f, Layer = NavLayer.Naval };

        // Stands in for fog tags, which move enemy buildings between archetypes without changing their footprints.
        private struct ArchetypeTag : IComponentData
        {
        }

        [SetUp]
        public void Prepare()
        {
            World.AddAgentPrefabs(Infantry, Truck, Tank, Boat);
        }

        [Test]
        public void DiagonalBox_CoversItsCentreButNotTheCornersOfItsBounds()
        {
            var box = new NavBox(float3.zero, quaternion.RotateY(math.PI * 0.25f), new float2(10f, 2f));
            var corner = box.Extents * 0.95f;

            Assert.IsFalse(box.IsAxisAligned);
            Assert.IsTrue(box.Contains(float2.zero, 0f));
            Assert.IsFalse(box.Contains(new float2(corner.x, -corner.y), 0f), "the empty corner of the world box");
            Assert.IsTrue(box.Contains(new float2(3f, -3f), 0f) || box.Contains(new float2(3f, 3f), 0f), "along its length");
        }

        [Test]
        public void Grid_KeepsItsVersion_WhenABuildingOnlyChangesArchetype_AndRebuildsWhenItMoves()
        {
            var site = new float3(20f, 0f, 20f);
            var building = World.SpawnBuilding(2, site, new float2(4f, 4f));
            World.Tick();
            var version = World.Grid().Version;

            World.EntityManager.AddComponent<ArchetypeTag>(building);
            World.Tick();
            Assert.AreEqual(version, World.Grid().Version, "same footprints, so paths stay valid");

            var moved = site + new float3(10f, 0f, 0f);
            var transform = World.Get<LocalTransform>(building);
            World.Set(building, transform.WithPosition(moved));
            World.EntityManager.RemoveComponent<ArchetypeTag>(building);
            World.Tick();

            var grid = World.Grid();
            Assert.Greater(grid.Version, version);
            Assert.IsTrue(grid.IsWalkable(site));
            Assert.IsFalse(grid.IsWalkable(moved));
        }

        private float GroundField(float x, float z)
        {
            var grid = World.Grid();
            return Infantry.Access(grid).Distance(grid.Grid.Cell(new float3(x, 0f, z)));
        }

        [Test]
        public void Field_IsNegativeInsideWalls_AboutZeroOnTheirEdge_AndFollowsObstacles()
        {
            var obstacle = World.SpawnObstacle(new float3(0.5f, 0f, 0.5f), new float2(1f, 1f));
            World.SpawnObstacle(new float3(50f, 0f, 50f), new float2(20f, 20f));
            World.Tick();

            Assert.AreEqual(-0.5f, GroundField(0.5f, 0.5f), 1e-4f, "the closed cell");
            Assert.AreEqual(0.5f, GroundField(-0.5f, 0.5f), 1e-4f, "beside it");
            Assert.AreEqual(math.SQRT2 - 0.5f, GroundField(-0.5f, -0.5f), 1e-4f, "diagonally below it");
            Assert.AreEqual(2.5f, GroundField(-2.5f, 0.5f), 1e-4f, "three cells out");
            Assert.AreEqual(-NavField.MaxDistance, GroundField(50f, 50f), "deep inside a building, capped");
            var access = Infantry.Access(World.Grid());
            Assert.AreEqual(0f, access.Distance(new float2(0f, 0.5f)), 1e-4f, "on its edge");
            MathAssert.Near(new float2(-1f, 0f), access.Normal(new float2(-0.2f, 0.5f)));

            World.EntityManager.DestroyEntity(obstacle);
            World.Tick();

            Assert.AreEqual(NavField.MaxDistance, GroundField(0.5f, 0.5f), "reopened once the obstacle is gone");
            Assert.AreEqual(NavField.MaxDistance, GroundField(-0.5f, -0.5f));
        }

        [Test]
        public void GapExactlyTwoRadiiWide_LetsThatRadiusThrough()
        {
            // A wall along x with a three-cell gap from x -1 to 2, so the middle cell is 1.5 m from each side.
            World.SpawnObstacle(new float3(-51f, 0f, 0.5f), new float2(100f, 1f));
            World.SpawnObstacle(new float3(52f, 0f, 0.5f), new float2(100f, 1f));
            var exact = new NavAgent { Radius = 1.5f };
            var wider = new NavAgent { Radius = 1.6f };
            World.AddAgentPrefabs(exact, wider);
            World.Tick();

            var grid = World.Grid();
            var south = new float3(0.5f, 0f, -10f);
            var north = new float3(0.5f, 0f, 10f);
            Assert.IsTrue(exact.Access(grid).IsOpen(grid.Grid.Cell(new float3(0.5f, 0f, 0.5f))), "the gap's middle");
            Assert.IsTrue(grid.IsReachable(exact, south, north), "through the gap");
            Assert.IsFalse(grid.IsReachable(wider, south, north), "a hair wider can't pass");
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
