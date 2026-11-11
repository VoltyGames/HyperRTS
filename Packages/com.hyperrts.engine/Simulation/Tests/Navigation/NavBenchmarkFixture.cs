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
    public abstract class NavBenchmarkFixture : SimulationFixture
    {
        protected static readonly NavAgent Infantry = new() { Radius = 0.5f, SceneryCrushLimit = NavAccess.AllScenery };
        protected static readonly NavAgent Truck = new() { Radius = 1.2f, WallMargin = 3.5f };
        protected static readonly NavAgent Tank = new() { Radius = 1.85f, SceneryCrushLimit = 3 };
        protected static readonly NavAgent Carrier = new() { Radius = 1.4f, SceneryCrushLimit = 3 };
        protected static readonly NavAgent Boat = new() { Radius = 1f, Layer = NavLayer.Naval };

        protected override float MapSize => 600f;

        [SetUp]
        public void BuildMap()
        {
            World.AddAgentPrefabs(Infantry, Truck, Tank, Carrier, Boat);
            // A river north to south, crossed by two bridges.
            World.SpawnNavArea(new float3(100f, 0f, 0f), new float2(20f, MapSize), NavAreaKind.Water);
            World.SpawnNavArea(new float3(100f, 0f, -150f), new float2(24f, 10f), NavAreaKind.Deck);
            World.SpawnNavArea(new float3(100f, 0f, 120f), new float2(24f, 10f), NavAreaKind.Deck);
            var random = new Random(17);
            SpawnBuildings(ref random, 80);
            SpawnWoods(ref random, 6, 200);
            World.Tick();
        }

        // Clear of the river and of the west and east strips where units start and end.
        protected static bool IsInland(float3 point) => math.abs(point.x) < 230f && math.abs(point.x - 100f) > 30f;

        protected void SpawnBuildings(ref Random random, int count)
        {
            for (var placed = 0; placed < count;)
            {
                var center = new float3(random.NextFloat(-280f, 280f), 0f, random.NextFloat(-280f, 280f));
                if (IsInland(center))
                {
                    World.SpawnObstacle(center, random.NextFloat2(new float2(8f), new float2(30f)),
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
            var tree = World.EntityManager.CreateEntity();
            World.Add(tree, LocalTransform.FromPosition(position));
            var writer = World.Writer(tree);
            DestructibleAuthoring.AddScenery(writer, 50f, crush, Entity.Null, float2.zero,
                SceneryId.At(position, quaternion.identity));
        }

        protected bool AllReady(Entity[] units)
        {
            foreach (var unit in units)
            {
                if (World.Get<PathState>(unit).Status != PathStatus.Ready)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
