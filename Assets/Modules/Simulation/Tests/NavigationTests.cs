using HyperRTS.Simulation.Navigation;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Selection;
using HyperRTS.Simulation.Units;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Tests
{
    /// <summary>Nav grid, pathfinding around buildings, formations and local avoidance.</summary>
    public class NavigationTests
    {
        private TestWorld _world;

        [SetUp]
        public void SetUp() => _world = new TestWorld();

        [TearDown]
        public void TearDown() => _world.Dispose();

        private NavGrid Grid()
        {
            _world.EntityManager.CompleteAllTrackedJobs();
            using var query = _world.EntityManager.CreateEntityQuery(typeof(NavGrid));
            return query.GetSingleton<NavGrid>();
        }

        private float3 PositionOf(Entity entity) => _world.Get<LocalTransform>(entity).Position;

        private void Move(float3 goal, params Entity[] units)
        {
            foreach (var unit in units)
            {
                _world.EntityManager.SetComponentEnabled<Selected>(unit, true);
            }

            _world.Command(1, new PlayerCommand { Type = CommandType.Smart, Position = goal });
        }

        [Test]
        public void Path_RoutesAroundWallOfBuildings_NeverEnteringBlockedCells()
        {
            _world.CreateMatch(1, 2);
            for (var z = -8f; z <= 8f; z += 4f)
            {
                _world.SpawnBuilding(1, new float3(0f, 0f, z), new float2(4f, 4f));
            }

            var unit = _world.SpawnUnit(1, new float3(-8f, 0f, 0f));
            var goal = new float3(8f, 0f, 0f);
            Move(goal, unit);

            var widest = 0f;
            for (var t = 0f; t < 12f; t += TestWorld.FrameTime)
            {
                _world.Tick();
                var position = PositionOf(unit);
                Assert.IsTrue(Grid().IsWalkable(position), $"entered a blocked cell at {position}");
                widest = math.max(widest, math.abs(position.z));
            }

            Assert.Less(math.distance(PositionOf(unit).xz, goal.xz), 0.2f);
            Assert.Greater(widest, 10f, "the unit went around the wall's end");
        }

        [Test]
        public void Formation_SpreadsGroupAroundGoal_WithoutStacking()
        {
            _world.CreateMatch(1, 2);
            var units = new Entity[9];
            for (var i = 0; i < units.Length; i++)
            {
                units[i] = _world.SpawnUnit(1, new float3(-10f + i % 3 * 1.2f, 0f, i / 3 * 1.2f));
            }

            var goal = new float3(10f, 0f, 0f);
            Move(goal, units);
            _world.Run(10f);

            for (var i = 0; i < units.Length; i++)
            {
                var position = PositionOf(units[i]);
                Assert.IsFalse(_world.IsEnabled<ActiveOrder>(units[i]), "every move order completed");
                Assert.Less(math.distance(position.xz, goal.xz), 4f);
                for (var j = i + 1; j < units.Length; j++)
                {
                    Assert.Greater(math.distance(position.xz, PositionOf(units[j]).xz), 0.9f, "units stacked");
                }
            }
        }

        [Test]
        public void Grid_RebuildsWhenBuildingIsAdded()
        {
            _world.CreateMatch(1, 2);
            _world.Tick();
            var site = new float3(20f, 0f, 20f);
            var before = Grid();
            Assert.IsTrue(before.IsWalkable(site));

            _world.SpawnBuilding(2, site, new float2(4f, 4f), complete: false);
            _world.Tick();

            var after = Grid();
            Assert.Greater(after.Version, before.Version);
            Assert.IsFalse(after.IsWalkable(site), "buildings under construction block too");
            Assert.IsTrue(after.IsWalkable(site + new float3(2.5f, 0f, 0f)));
        }

        [Test]
        public void MoveIntoBuilding_StopsAtNearestOpenGround()
        {
            _world.CreateMatch(1, 2);
            var building = new float3(10f, 0f, 0f);
            _world.SpawnBuilding(2, building, new float2(6f, 6f));
            var unit = _world.SpawnUnit(1, float3.zero);

            Move(building, unit);
            _world.Run(6f);

            Assert.IsFalse(_world.IsEnabled<ActiveOrder>(unit));
            Assert.IsTrue(Grid().IsWalkable(PositionOf(unit)));
            Assert.Less(math.distance(PositionOf(unit).xz, building.xz), 5f);
        }

        [Test]
        public void WithoutGrid_UnitMovesStraightToDestination()
        {
            var unit = _world.SpawnUnit(1, float3.zero);
            var goal = new float3(6f, 0f, 3f);
            _world.EntityManager.SetComponentData(unit, new MoveDestination { Value = goal });
            _world.EntityManager.SetComponentEnabled<MoveDestination>(unit, true);

            for (var t = 0f; t < 3f; t += TestWorld.FrameTime)
            {
                _world.Tick();
                var position = PositionOf(unit).xz;
                var offLine = math.abs(position.x * goal.z - position.y * goal.x) / math.length(goal.xz);
                Assert.Less(offLine, 0.01f, "straight line");
            }

            _world.EntityManager.CompleteAllTrackedJobs();
            using var grids = _world.EntityManager.CreateEntityQuery(typeof(NavGrid));
            Assert.AreEqual(0, grids.CalculateEntityCount());
            Assert.Less(math.distance(PositionOf(unit).xz, goal.xz), 0.2f);
            Assert.IsFalse(_world.IsEnabled<MoveDestination>(unit));
        }
    }
}
