using HyperRTS.Simulation.Common.Authoring;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Destruction;
using HyperRTS.Simulation.Navigation.Agents;
using HyperRTS.Simulation.Navigation.Areas;
using HyperRTS.Simulation.Navigation.Grid;
using HyperRTS.Simulation.Navigation.Paths;
using NUnit.Framework;
using Unity.PerformanceTesting;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Tests.Navigation
{
    /// <summary>
    /// Navigation at scale on a 600 m map at 1 m cells with about 80 rotated buildings, woods and a river with two
    /// bridges. Timings are recorded for the performance results, never asserted: they vary with the machine and Editor.
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
            _world.Add(tree, LocalTransform.FromPosition(position));
            var writer = _world.Writer(tree);
            DestructibleAuthoring.AddScenery(writer, 50f, crush, Entity.Null, float2.zero,
                SceneryId.At(position, quaternion.identity));
        }

        [Test, Performance]
        public void GroupOrder_CrossMap_GivesEveryMemberAPath()
        {
            const int count = 200;
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
                _world.Send(units[i], slot, group);
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
