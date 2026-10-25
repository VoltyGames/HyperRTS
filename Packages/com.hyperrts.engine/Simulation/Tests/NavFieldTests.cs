using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Navigation;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Tests
{
    /// <summary>
    /// The distance field: sector rebuilds after buildings and scenery change match a whole build, values are signed
    /// across walls, and a gap exactly as wide as a unit lets it through.
    /// </summary>
    public class NavFieldTests
    {
        private static readonly NavAgent Infantry = new() { Radius = 0.5f, SceneryCrushLimit = NavAccess.AllScenery };
        private static readonly NavAgent Truck = new() { Radius = 1.2f };
        private static readonly NavAgent Tank = new() { Radius = 1.6f, SceneryCrushLimit = 3 };
        private static readonly NavAgent Boat = new() { Radius = 1f, Layer = NavLayer.Naval };

        private TestWorld _world;

        [SetUp]
        public void SetUp()
        {
            _world = new TestWorld();
            _world.CreateMatch(1, 2);
            _world.AddAgentPrefabs(Infantry, Truck, Tank, Boat);
        }

        [TearDown]
        public void TearDown() => _world.Dispose();

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
        public void SectorRebuilds_AfterBuildingsAndSceneryChange_MatchAWholeBuild()
        {
            _world.SpawnNavArea(new float3(40f, 0f, 0f), new float2(20f, 200f), NavAreaKind.Water);
            var random = new Random(7);
            var placed = new NativeList<Entity>(Allocator.Temp);
            for (var round = 0; round < 6; round++)
            {
                for (var i = 0; i < 4; i++)
                {
                    var center = new float3(random.NextFloat(-90f, 90f), 0f, random.NextFloat(-90f, 90f));
                    var size = random.NextFloat2(new float2(2f), new float2(14f));
                    placed.Add(_world.SpawnObstacle(center, size, random.NextFloat(math.PI)));
                    placed.Add(PlantTree(center + new float3(9f, 0f, 3f), random.NextInt(1, 6)));
                }

                if (round > 1)
                {
                    _world.EntityManager.DestroyEntity(placed[random.NextInt(placed.Length)]);
                }

                _world.Tick();
                AssertMatchesWholeBuild($"round {round}");
            }

            placed.Dispose();
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
                Assert.AreEqual(whole.Field.Distances.Length, grid.Field.Distances.Length, when);
                for (var i = 0; i < whole.Field.Distances.Length; i++)
                {
                    if (whole.Field.Distances[i] != grid.Field.Distances[i])
                    {
                        Assert.Fail($"{when}: value {i} is {grid.Field.Distances[i]}, not {whole.Field.Distances[i]}");
                    }
                }

                CollectionAssert.AreEqual(whole.Sectors.Labels.ToArray(), grid.Sectors.Labels.ToArray(), when);
                CollectionAssert.AreEqual(whole.Sectors.Regions.AsArray().ToArray(),
                    grid.Sectors.Regions.AsArray().ToArray(), when);
            }
            finally
            {
                whole.Dispose();
            }
        }

        [Test]
        public void Values_AreNegativeInsideABuilding_AndPositiveOutside()
        {
            _world.SpawnObstacle(float3.zero, new float2(20f, 20f));
            _world.Tick();
            var grid = _world.Grid();
            var access = Truck.Access(grid);

            Assert.AreEqual(-NavField.Cap, access.Distance(grid.WorldToCell(float3.zero)), "deep inside, capped");
            Assert.AreEqual(-0.5f, access.Distance(grid.WorldToCell(new float3(9.5f, 0f, 0f))), 1e-4f, "its rim");
            Assert.AreEqual(0.5f, access.Distance(grid.WorldToCell(new float3(10.5f, 0f, 0f))), 1e-4f, "beside it");
            Assert.AreEqual(4.5f, access.Distance(grid.WorldToCell(new float3(14.5f, 0f, 0f))), 1e-4f);
            Assert.Greater(access.Normal(new float2(9.8f, 0f)).x, 0.99f, "the normal points out of it");
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
    }
}
