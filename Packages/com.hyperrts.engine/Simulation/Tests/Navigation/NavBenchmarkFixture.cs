using HyperRTS.Simulation.Common.Authoring;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Destruction;
using HyperRTS.Simulation.Navigation.Agents;
using HyperRTS.Simulation.Navigation.Areas;
using HyperRTS.Simulation.Navigation.Grid;
using HyperRTS.Simulation.Navigation.Paths;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Tests.Navigation
{
    /// <summary>Shared setup and helpers for the NavBenchmarkTests family.</summary>
    [Category("Benchmark")]
    public abstract class NavBenchmarkFixture
    {
        protected const float MapSize = 600f;

        protected static readonly NavAgent Infantry = new() { Radius = 0.5f, SceneryCrushLimit = NavAccess.AllScenery };
        protected static readonly NavAgent Truck = new() { Radius = 1.2f, WallMargin = 3.5f };
        protected static readonly NavAgent Tank = new() { Radius = 1.85f, SceneryCrushLimit = 3 };
        protected static readonly NavAgent Carrier = new() { Radius = 1.4f, SceneryCrushLimit = 3 };
        protected static readonly NavAgent Boat = new() { Radius = 1f, Layer = NavLayer.Naval };

        protected TestWorld _world;

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
        protected static bool IsInland(float3 point) => math.abs(point.x) < 230f && math.abs(point.x - 100f) > 30f;

        protected void SpawnBuildings(ref Random random, int count)
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

        protected void SpawnWoods(ref Random random, int woods, int trees)
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

        protected void PlantTree(float3 position, int crush)
        {
            var tree = _world.EntityManager.CreateEntity();
            _world.Add(tree, LocalTransform.FromPosition(position));
            var writer = _world.Writer(tree);
            DestructibleAuthoring.AddScenery(writer, 50f, crush, Entity.Null, float2.zero,
                SceneryId.At(position, quaternion.identity));
        }

        protected PathStats Stats()
        {
            _world.EntityManager.CompleteAllTrackedJobs();
            var handle = _world.World.GetExistingSystem<PathfindingSystem>();
            return _world.World.Unmanaged.GetUnsafeSystemRef<PathfindingSystem>(handle).Stats;
        }

        protected bool AllReady(Entity[] units)
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
