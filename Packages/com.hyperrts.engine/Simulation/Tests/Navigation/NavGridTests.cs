using System.Collections.Generic;
using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Navigation;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Tests.Navigation
{
    /// <summary>
    /// The nav grid: obstacle boxes, rebuilds when buildings change, the signed distance field, and sector regions
    /// that match a flood fill and, rebuilt piece by piece, a whole build.
    /// </summary>
    public class NavGridTests
    {
        private static readonly NavAgent Infantry = new() { Radius = 0.5f, SceneryCrushLimit = NavAccess.AllScenery };
        private static readonly NavAgent Truck = new() { Radius = 1.2f };
        private static readonly NavAgent Tank = new() { Radius = 1.85f, SceneryCrushLimit = 3 };
        private static readonly NavAgent Boat = new() { Radius = 1f, Layer = NavLayer.Naval };

        private TestWorld _world;

        // Stands in for fog tags, which move enemy buildings between archetypes without changing their footprints.
        private struct ArchetypeTag : IComponentData
        {
        }

        [SetUp]
        public void SetUp()
        {
            _world = new TestWorld();
            _world.CreateMatch(1, 2);
            _world.AddAgentPrefabs(Infantry, Truck, Tank, Boat);
        }

        [TearDown]
        public void TearDown() => _world.Dispose();

        [Test]
        public void QuarterTurnedBox_SwapsWidthAndDepth_AndStaysAxisAligned()
        {
            var box = new NavBox(float3.zero, quaternion.RotateY(math.PI * 0.5f), new float2(10f, 4f));

            Assert.IsTrue(box.IsAxisAligned);
            Assert.AreEqual(2f, box.Extents.x, 1e-4f);
            Assert.AreEqual(5f, box.Extents.y, 1e-4f);
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
        public void Grid_RebuildsWhenBuildingIsAdded()
        {
            _world.Tick();
            var site = new float3(20f, 0f, 20f);
            var before = _world.Grid();
            Assert.IsTrue(before.IsWalkable(site));

            _world.SpawnBuilding(2, site, new float2(4f, 4f), complete: false);
            _world.Tick();

            var after = _world.Grid();
            Assert.Greater(after.Version, before.Version);
            Assert.IsFalse(after.IsWalkable(site), "buildings under construction block too");
            Assert.IsTrue(after.IsWalkable(site + new float3(2.5f, 0f, 0f)));
        }

        [Test]
        public void Grid_KeepsItsVersion_WhenABuildingOnlyChangesArchetype_AndRebuildsWhenItMoves()
        {
            var site = new float3(20f, 0f, 20f);
            var building = _world.SpawnBuilding(2, site, new float2(4f, 4f));
            _world.Tick();
            var version = _world.Grid().Version;

            _world.EntityManager.AddComponent<ArchetypeTag>(building);
            _world.Tick();
            Assert.AreEqual(version, _world.Grid().Version, "same footprints, so paths stay valid");

            var moved = site + new float3(10f, 0f, 0f);
            var transform = _world.EntityManager.GetComponentData<LocalTransform>(building);
            _world.EntityManager.SetComponentData(building, transform.WithPosition(moved));
            _world.EntityManager.RemoveComponent<ArchetypeTag>(building);
            _world.Tick();

            var grid = _world.Grid();
            Assert.Greater(grid.Version, version);
            Assert.IsTrue(grid.IsWalkable(site));
            Assert.IsFalse(grid.IsWalkable(moved));
        }

        private float GroundField(float x, float z)
        {
            var grid = _world.Grid();
            return Infantry.Access(grid).Distance(grid.WorldToCell(new float3(x, 0f, z)));
        }

        [Test]
        public void Field_IsNegativeInsideWalls_AboutZeroOnTheirEdge_AndFollowsObstacles()
        {
            var obstacle = _world.SpawnObstacle(new float3(0.5f, 0f, 0.5f), new float2(1f, 1f));
            _world.SpawnObstacle(new float3(50f, 0f, 50f), new float2(20f, 20f));
            _world.Tick();

            Assert.AreEqual(-0.5f, GroundField(0.5f, 0.5f), 1e-4f, "the closed cell");
            Assert.AreEqual(0.5f, GroundField(-0.5f, 0.5f), 1e-4f, "beside it");
            Assert.AreEqual(math.SQRT2 - 0.5f, GroundField(-0.5f, -0.5f), 1e-4f, "diagonally below it");
            Assert.AreEqual(2.5f, GroundField(-2.5f, 0.5f), 1e-4f, "three cells out");
            Assert.AreEqual(-NavField.MaxDistance, GroundField(50f, 50f), "deep inside a building, capped");
            var access = Infantry.Access(_world.Grid());
            Assert.AreEqual(0f, access.Distance(new float2(0f, 0.5f)), 1e-4f, "on its edge");
            MathAssert.Near(new float2(-1f, 0f), access.Normal(new float2(-0.2f, 0.5f)));

            _world.EntityManager.DestroyEntity(obstacle);
            _world.Tick();

            Assert.AreEqual(NavField.MaxDistance, GroundField(0.5f, 0.5f), "reopened once the obstacle is gone");
            Assert.AreEqual(NavField.MaxDistance, GroundField(-0.5f, -0.5f));
        }

        [Test]
        public void GapExactlyTwoRadiiWide_LetsThatRadiusThrough()
        {
            // A wall along x with a three-cell gap from x -1 to 2, so the middle cell is 1.5 m from each side.
            _world.SpawnObstacle(new float3(-51f, 0f, 0.5f), new float2(100f, 1f));
            _world.SpawnObstacle(new float3(52f, 0f, 0.5f), new float2(100f, 1f));
            var exact = new NavAgent { Radius = 1.5f };
            var wider = new NavAgent { Radius = 1.6f };
            _world.AddAgentPrefabs(exact, wider);
            _world.Tick();

            var grid = _world.Grid();
            var south = new float3(0.5f, 0f, -10f);
            var north = new float3(0.5f, 0f, 10f);
            Assert.IsTrue(exact.Access(grid).IsOpen(grid.WorldToCell(new float3(0.5f, 0f, 0.5f))), "the gap's middle");
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
                placed.Add(_world.SpawnObstacle(center, size, random.NextFloat(math.PI)));
            }

            return placed;
        }

        private Entity PlantTree(float3 position, int crush)
        {
            var tree = _world.EntityManager.CreateEntity();
            _world.EntityManager.AddComponentData(tree, LocalTransform.FromPosition(position));
            var writer = new EntityManagerWriter(_world.EntityManager, tree);
            DestructibleSetup.AddScenery(ref writer, 50f, crush, Entity.Null, float2.zero,
                SceneryId.At(position, quaternion.identity));
            return tree;
        }

        [Test]
        public void Regions_MatchAFloodFillOverOpenCells()
        {
            _world.SpawnNavArea(new float3(0f, 0f, 30f), new float2(200f, 12f), NavAreaKind.Water);
            _world.SpawnNavArea(new float3(-40f, 0f, 30f), new float2(6f, 14f), NavAreaKind.Deck);
            // A closed ring, so its inside is a region of its own.
            _world.SpawnObstacle(new float3(50f, 0f, -50f), new float2(20f, 2f));
            _world.SpawnObstacle(new float3(50f, 0f, -70f), new float2(20f, 2f));
            _world.SpawnObstacle(new float3(41f, 0f, -60f), new float2(2f, 20f));
            _world.SpawnObstacle(new float3(59f, 0f, -60f), new float2(2f, 20f));
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
        public void Rebuilds_AfterBuildingsAndSceneryChange_MatchAWholeBuild()
        {
            _world.SpawnNavArea(new float3(40f, 0f, 0f), new float2(20f, 200f), NavAreaKind.Water);
            var placed = SpawnClutter(5, 10);
            _world.Tick();
            var random = new Random(9);
            for (var round = 0; round < 6; round++)
            {
                placed.AddRange(SpawnClutter(random.NextUInt(1, uint.MaxValue), 3));
                for (var i = 0; i < 3; i++)
                {
                    var spot = new float3(random.NextFloat(-90f, 90f), 0f, random.NextFloat(-90f, 90f));
                    placed.Add(PlantTree(spot, random.NextInt(1, 6)));
                }

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
