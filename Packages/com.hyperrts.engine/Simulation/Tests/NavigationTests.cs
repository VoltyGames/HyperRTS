using System;
using HyperRTS.Simulation.Navigation;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Selection;
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

        // Stands in for fog tags, which move enemy buildings between archetypes without changing their footprints.
        private struct ArchetypeTag : IComponentData
        {
        }

        [SetUp]
        public void SetUp() => _world = new TestWorld();

        [TearDown]
        public void TearDown() => _world.Dispose();

        private void Move(float3 goal, params Entity[] units)
        {
            foreach (var unit in units)
            {
                _world.EntityManager.SetComponentEnabled<Selected>(unit, true);
            }

            _world.Command(1, new PlayerCommand { Type = CommandType.Smart, Position = goal });
        }

        private bool Arrived(Entity unit, float3 goal)
        {
            if (_world.IsEnabled<ActiveOrder>(unit))
            {
                return false;
            }

            return math.distance(_world.GetPosition(unit).xz, goal.xz) < 4f;
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
                var position = _world.GetPosition(unit);
                Assert.IsTrue(_world.Grid().IsWalkable(position), $"entered a blocked cell at {position}");
                widest = math.max(widest, math.abs(position.z));
            }

            Assert.Less(math.distance(_world.GetPosition(unit).xz, goal.xz), 0.2f);
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
            _world.RunUntil(() => Array.TrueForAll(units, unit => Arrived(unit, goal)), 10f);

            for (var i = 0; i < units.Length; i++)
            {
                var position = _world.GetPosition(units[i]);
                Assert.IsFalse(_world.IsEnabled<ActiveOrder>(units[i]), "every move order completed");
                Assert.Less(math.distance(position.xz, goal.xz), 4f);
                for (var j = i + 1; j < units.Length; j++)
                {
                    Assert.Greater(math.distance(position.xz, _world.GetPosition(units[j]).xz), 0.9f, "units stacked");
                }
            }
        }

        [Test]
        public void Formation_IntoABuilding_PutsEverySlotOnOpenGround()
        {
            _world.CreateMatch(1, 2);
            var goal = new float3(10f, 0f, 0f);
            _world.SpawnBuilding(2, goal, new float2(8f, 8f));
            _world.Tick();
            var units = new Entity[4];
            for (var i = 0; i < units.Length; i++)
            {
                units[i] = _world.SpawnUnit(1, new float3(-10f, 0f, i * 1.5f));
            }

            Move(goal, units);
            _world.Tick();

            foreach (var unit in units)
            {
                var slot = _world.Get<ActiveOrder>(unit).Value.Position;
                Assert.IsTrue(_world.Grid().IsWalkable(slot), "the slot moved out of the building");
            }
        }

        [Test]
        public void Grid_RebuildsWhenBuildingIsAdded()
        {
            _world.CreateMatch(1, 2);
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
            _world.CreateMatch(1, 2);
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
            Assert.IsTrue(_world.Grid().IsWalkable(_world.GetPosition(unit)));
            Assert.Less(math.distance(_world.GetPosition(unit).xz, building.xz), 5f);
        }
    }
}
