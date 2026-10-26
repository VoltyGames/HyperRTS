using System.Diagnostics;
using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Navigation;
using NUnit.Framework;
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
    /// bridges, against twice the plan's budgets. Each test runs its work once untimed first, so Burst is warm.
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
            var writer = new EntityManagerWriter(_world.EntityManager, tree);
            DestructibleSetup.AddScenery(ref writer, 50f, crush, Entity.Null, float2.zero,
                SceneryId.At(position, quaternion.identity));
        }

        private double TimeSystem<T>() where T : unmanaged, ISystem
        {
            var watch = Stopwatch.StartNew();
            _world.World.GetExistingSystem<T>().Update(_world.World.Unmanaged);
            _world.EntityManager.CompleteAllTrackedJobs();
            return watch.Elapsed.TotalMilliseconds;
        }

        [Test]
        public void FieldAndSectorBuilds_FullAndAfterABuilding()
        {
            var full = double.MaxValue;
            for (var run = 0; run < 4; run++)
            {
                var grid = _world.Grid();
                grid.Field.MarkDirty(int2.zero, grid.Size - 1);
                var time = TimeSystem<NavFieldSystem>();
                full = run == 0 ? full : math.min(full, time);
            }

            var restamp = 0.0;
            var incremental = 0.0;
            var random = new Random(5);
            for (var run = 0; run < 6; run++)
            {
                _world.SpawnObstacle(new float3(random.NextFloat(-200f, 60f), 0f, random.NextFloat(-250f, 250f)),
                    new float2(14f, 10f), random.NextFloat(math.PI));
                var grid = TimeSystem<NavGridSystem>();
                var field = TimeSystem<NavFieldSystem>();
                restamp += run == 0 ? 0.0 : grid / 5.0;
                incremental += run == 0 ? 0.0 : field / 5.0;
            }

            var sectors = _world.Grid().Sectors;
            TestContext.WriteLine($"Full field and sector build: {full:F2} ms ({sectors.ClassCount} classes, " +
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

        [Test]
        public void CrossMapSearches_TwoHundred()
        {
            var points = new NativeList<float3>(64, Allocator.Persistent);
            var expansions = new NativeReference<int>(Allocator.Persistent);
            var job = new SearchJob { Grid = _world.Grid(), Agent = Truck, Points = points, Expansions = expansions };
            var random = new Random(23);
            double total = 0.0, slowest = 0.0, length = 0.0;
            long expanded = 0;
            var most = 0;
            for (var i = -1; i < 200; i++)
            {
                job.Start = new float3(random.NextFloat(-290f, -250f), 0f, random.NextFloat(-290f, 290f));
                job.Goal = new float3(random.NextFloat(250f, 290f), 0f, random.NextFloat(-290f, 290f));
                var watch = Stopwatch.StartNew();
                job.Run();
                var time = watch.Elapsed.TotalMilliseconds;
                if (i >= 0)
                {
                    total += time;
                    slowest = math.max(slowest, time);
                    expanded += expansions.Value;
                    most = math.max(most, expansions.Value);
                    length += PathLength(job.Start, points);
                }
            }

            points.Dispose();
            expansions.Dispose();
            TestContext.WriteLine($"200 cross-map searches: mean {total / 200.0:F3} ms, max {slowest:F3} ms, " +
                                  $"expansions mean {expanded / 200} max {most}, path mean {length / 200.0:F1} m");
            Assert.Less(total / 200.0, 4.0, $"mean {total / 200.0:F3} ms, under twice 2 ms");
            Assert.Less(slowest, 8.0, $"slowest {slowest:F3} ms, under four times 2 ms");
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

        [TestCase(30, 12.0)]
        [TestCase(200, 20.0)]
        public void GroupOrder_CrossMap(int count, double budget)
        {
            Order(count, 1);
            Order(count, 2);
            var before = Stats();
            var units = Order(count, 3);
            var after = Stats();
            TestContext.WriteLine($"{count}-unit group order: {_served:F2} ms over {_frames} frames, " +
                                  $"{after.Fields - before.Fields} fields, {after.Traces - before.Traces} traces, " +
                                  $"{after.Searches - before.Searches} searches");
            foreach (var unit in units)
            {
                Assert.AreEqual(PathStatus.Ready, _world.Get<PathState>(unit).Status, "every member has a path");
            }

            Assert.Less(_served, budget, $"{_served:F2} ms, under twice the budget");
        }

        private double _served;
        private int _frames;

        private PathStats Stats()
        {
            _world.EntityManager.CompleteAllTrackedJobs();
            var handle = _world.World.GetExistingSystem<PathfindingSystem>();
            return _world.World.Unmanaged.GetUnsafeSystemRef<PathfindingSystem>(handle).Stats;
        }

        // A block of units on the west edge sent to a block of slots on the east edge, timing only the pathfinder.
        private Entity[] Order(int count, int group)
        {
            var side = (int)math.ceil(math.sqrt(count));
            var units = new Entity[count];
            for (var i = 0; i < count; i++)
            {
                var start = new float3(-285f + i % side * 3f, 0f, -20f + i / side * 3f + group * 60f - 120f);
                units[i] = _world.SpawnUnit(1, start);
            }

            _world.Tick();
            for (var i = 0; i < count; i++)
            {
                var slot = new float3(245f + i % side * 3f, 0f, -20f + i / side * 3f + group * 50f - 100f);
                _world.EntityManager.SetComponentData(units[i], new MoveDestination { Value = slot, Group = group });
                _world.EntityManager.SetComponentEnabled<MoveDestination>(units[i], true);
            }

            _served = 0.0;
            for (_frames = 0; _frames < 20 && !AllReady(units); _frames++)
            {
                _served += TimeSystem<PathfindingSystem>();
            }

            return units;
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
