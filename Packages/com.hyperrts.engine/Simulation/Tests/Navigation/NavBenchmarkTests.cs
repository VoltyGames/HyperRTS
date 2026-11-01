using HyperRTS.Simulation.Common.Authoring;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Destruction;
using HyperRTS.Simulation.Navigation.Agents;
using HyperRTS.Simulation.Navigation.Areas;
using HyperRTS.Simulation.Navigation.Fields;
using HyperRTS.Simulation.Navigation.Grid;
using HyperRTS.Simulation.Navigation.Paths;
using NUnit.Framework;
using Unity.PerformanceTesting;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Tests.Navigation
{
    /// <summary>
    /// Navigation timings on a 600 m map at 1 m cells with about 80 rotated buildings, woods and a river with two
    /// bridges, against twice the plan's budgets. Each test warms up first, so Burst is compiled, and asserts the median.
    /// </summary>
    [Category("Benchmark")]
    public partial class NavBenchmarkTests
    {
        private const float MapSize = 600f;

        private static readonly NavAgent Infantry = new() { Radius = 0.5f, SceneryCrushLimit = NavAccess.AllScenery };
        private static readonly NavAgent Truck = new() { Radius = 1.2f, WallMargin = 3.5f };
        private static readonly NavAgent Tank = new() { Radius = 1.85f, SceneryCrushLimit = 3 };
        private static readonly NavAgent Carrier = new() { Radius = 1.4f, SceneryCrushLimit = 3 };
        private static readonly NavAgent Boat = new() { Radius = 1f, Layer = NavLayer.Naval };

        private TestWorld _world;

        [SetUp]
        public void SetUp()
        {
            _world = new TestWorld();
            _world.CreateMatch(new byte[] { 1, 2 }, MapSize);
            _world.AddAgentPrefabs(Infantry, Truck, Tank, Carrier, Boat);
            // A river north to south, crossed by two bridges.
            _world.SpawnNavArea(new float3(100f, 0f, 0f), new float2(20f, MapSize), NavAreaKind.Water);
            _world.SpawnNavArea(new float3(100f, 0f, -150f), new float2(24f, 10f), NavAreaKind.Deck);
            _world.SpawnNavArea(new float3(100f, 0f, 120f), new float2(24f, 10f), NavAreaKind.Deck);
            var random = new Random(17);
            SpawnBuildings(ref random, 80);
            SpawnWoods(ref random, 6, 200);
            _world.Tick();
        }

        [TearDown]
        public void TearDown() => _world.Dispose();

        // Clear of the river and of the west and east strips where units start and end.
        private static bool IsInland(float3 point) => math.abs(point.x) < 230f && math.abs(point.x - 100f) > 30f;

        private void SpawnBuildings(ref Random random, int count)
        {
            for (var placed = 0; placed < count;)
            {
                var center = new float3(random.NextFloat(-280f, 280f), 0f, random.NextFloat(-280f, 280f));
                if (IsInland(center))
                {
                    _world.SpawnObstacle(center, random.NextFloat2(new float2(8f), new float2(30f)),
                        random.NextFloat(math.PI));
                    placed++;
                }
            }
        }

        private void SpawnWoods(ref Random random, int woods, int trees)
        {
            for (var placed = 0; placed < woods;)
            {
                var center = new float3(random.NextFloat(-200f, 200f), 0f, random.NextFloat(-260f, 260f));
                if (!IsInland(center))
                {
                    continue;
                }

                for (var i = 0; i < trees; i++)
                {
                    var offset = random.NextFloat2Direction() * math.sqrt(random.NextFloat()) * 25f;
                    PlantTree(center + new float3(offset.x, 0f, offset.y), random.NextInt(1, 6));
                }

                placed++;
            }
        }

        private void PlantTree(float3 position, int crush)
        {
            var tree = _world.EntityManager.CreateEntity();
            _world.EntityManager.AddComponentData(tree, LocalTransform.FromPosition(position));
            var writer = new EntityWriter(_world.EntityManager, tree);
            DestructibleAuthoring.AddScenery(writer, 50f, crush, Entity.Null, float2.zero,
                SceneryId.At(position, quaternion.identity));
        }

        [Test, Performance]
        public void FieldAndSectorBuilds_FullAndAfterABuilding()
        {
            var full = BenchmarkKit.Median("FullBuild", _world.UpdateSystem<NavFieldSystem>, () =>
            {
                var grid = _world.Grid();
                grid.Field.MarkDirty(int2.zero, grid.Size - 1);
            }, null, "NavFieldSystem.Rebuild", "NavSectors.Relabel", "NavSectors.Connect", "FlowField.Build");

            var random = new Random(5);
            void AddBuilding() => _world.SpawnObstacle(
                new float3(random.NextFloat(-200f, 60f), 0f, random.NextFloat(-250f, 250f)), new float2(14f, 10f),
                random.NextFloat(math.PI));

            var incremental = BenchmarkKit.Median("AfterABuilding", _world.UpdateSystem<NavFieldSystem>, () =>
            {
                AddBuilding();
                _world.UpdateSystem<NavGridSystem>();
            }, null, "NavFieldSystem.Rebuild", "NavSectors.Relabel", "NavSectors.Connect");
            var restamp = BenchmarkKit.Median("Restamp", _world.UpdateSystem<NavGridSystem>, AddBuilding, null,
                "NavGridSystem.Restamp");

            var sectors = _world.Grid().Sectors;
            TestContext.WriteLine($"Full field and sector build: median {full:F2} ms ({sectors.ClassCount} classes, " +
                                  $"{sectors.SectorCount} sectors, {sectors.Regions.Length} components)");
            TestContext.WriteLine($"After a building: restamp {restamp:F2} ms, field and sectors {incremental:F2} ms");
            Assert.Less(full, 60.0, $"full build {full:F2} ms, under twice 30 ms");
            Assert.Less(incremental, 2.0, $"after a building {incremental:F2} ms, under twice 1 ms");
        }

        [BurstCompile(CompileSynchronously = true)]
        private struct SearchJob : IJob
        {
            [ReadOnly] public NavGrid Grid;
            public NavAgent Agent;
            public float3 Start;
            public float3 Goal;
            public NativeList<float3> Points;
            public NativeReference<int> Expansions;

            public void Execute()
            {
                Points.Clear();
                GridPathfinder.Plan(Agent.Access(Grid), Start, Goal, Points, out var expansions);
                Expansions.Value = expansions;
            }
        }

        [Test, Performance]
        public void CrossMapSearches()
        {
            var points = new NativeList<float3>(64, Allocator.Persistent);
            var expansions = new NativeReference<int>(Allocator.Persistent);
            var job = new SearchJob { Grid = _world.Grid(), Agent = Truck, Points = points, Expansions = expansions };
            var random = new Random(23);
            double length = 0.0;
            long expanded = 0;
            var most = 0;
            var median = BenchmarkKit.Median("CrossMapSearch", () => job.Run(), () =>
            {
                job.Start = new float3(random.NextFloat(-290f, -250f), 0f, random.NextFloat(-290f, 290f));
                job.Goal = new float3(random.NextFloat(250f, 290f), 0f, random.NextFloat(-290f, 290f));
            }, () =>
            {
                expanded += expansions.Value;
                most = math.max(most, expansions.Value);
                length += PathLength(job.Start, points);
            }, "GridAStar.Search");

            var runs = BenchmarkKit.Warmup + BenchmarkKit.Runs;
            points.Dispose();
            expansions.Dispose();
            TestContext.WriteLine($"Cross-map search: median {median:F3} ms, expansions mean {expanded / runs} " +
                                  $"max {most}, path mean {length / runs:F1} m");
            Assert.Less(median, 4.0, $"median {median:F3} ms, under twice 2 ms");
        }

        private static float PathLength(float3 start, NativeList<float3> points)
        {
            var length = 0f;
            for (var i = 0; i < points.Length; i++)
            {
                length += math.distance((i == 0 ? start : points[i - 1]).xz, points[i].xz);
            }

            return length;
        }

        [Test, Performance]
        [TestCase(30, 12.0)]
        [TestCase(200, 20.0)]
        public void GroupOrder_CrossMap(int count, double budget)
        {
            var group = 0;
            Entity[] units = null;
            var before = Stats();
            var median = BenchmarkKit.Median($"GroupOrder{count}", () => Serve(units),
                () => units = Order(count, ++group), () =>
                {
                    foreach (var unit in units)
                    {
                        Assert.AreEqual(PathStatus.Ready, _world.Get<PathState>(unit).Status, "every member has a path");
                        _world.EntityManager.DestroyEntity(unit);
                    }
                }, "FlowField.Build", "FlowField.Trace", "GridAStar.Search");

            var after = Stats();
            TestContext.WriteLine($"{count}-unit group order: median {median:F2} ms, " +
                                  $"{after.Fields - before.Fields} fields, {after.Traces - before.Traces} traces, " +
                                  $"{after.Searches - before.Searches} searches");
            Assert.Less(median, budget, $"{median:F2} ms, under twice the budget");
        }

        private PathStats Stats()
        {
            _world.EntityManager.CompleteAllTrackedJobs();
            var handle = _world.World.GetExistingSystem<PathfindingSystem>();
            return _world.World.Unmanaged.GetUnsafeSystemRef<PathfindingSystem>(handle).Stats;
        }

        // A block of units on the west edge sent to a block of slots on the east edge, a new place each group.
        private Entity[] Order(int count, int group)
        {
            var side = (int)math.ceil(math.sqrt(count));
            var place = group % 6;
            var units = new Entity[count];
            for (var i = 0; i < count; i++)
            {
                var start = new float3(-285f + i % side * 3f, 0f, -20f + i / side * 3f + place * 60f - 120f);
                units[i] = _world.SpawnUnit(1, start);
            }

            _world.Tick();
            for (var i = 0; i < count; i++)
            {
                var slot = new float3(245f + i % side * 3f, 0f, -20f + i / side * 3f + place * 50f - 100f);
                _world.EntityManager.SetComponentData(units[i], new MoveDestination { Value = slot, Group = group });
                _world.EntityManager.SetComponentEnabled<MoveDestination>(units[i], true);
            }

            return units;
        }

        // Only the pathfinder runs, until every unit has a path.
        private void Serve(Entity[] units)
        {
            for (var frame = 0; frame < 20 && !AllReady(units); frame++)
            {
                _world.UpdateSystem<PathfindingSystem>();
            }
        }

        private bool AllReady(Entity[] units)
        {
            foreach (var unit in units)
            {
                if (_world.Get<PathState>(unit).Status != PathStatus.Ready)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
