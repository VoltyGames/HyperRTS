using HyperRTS.Simulation.Buildings;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Navigation;
using HyperRTS.Simulation.Orders;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Tests
{
    /// <summary>Nav layers over land and water, shoreline placement, and bridge decks that can be destroyed.</summary>
    public class NavalTests
    {
        private const float WaterLevel = -1f;

        private TestWorld _world;

        [SetUp]
        public void SetUp()
        {
            _world = new TestWorld();
            _world.CreateMatch(1, 2);
            _world.ConfigureMap(WaterLevel);
        }

        [TearDown]
        public void TearDown() => _world.Dispose();

        private MapSettings Map()
        {
            using var query = _world.EntityManager.CreateEntityQuery(typeof(MapSettings));
            return query.GetSingleton<MapSettings>();
        }

        [Test]
        public void GroundUnits_RouteAroundWater_WhileShipsCrossIt()
        {
            _world.SpawnNavArea(float3.zero, new float2(10f, 40f), NavAreaKind.Water);
            var tank = _world.SpawnUnit(1, new float3(-12f, 0f, 0f), speed: 10f);
            var ship = _world.SpawnShip(1, new float3(0f, 0f, -15f));
            _world.MoveTo(tank, new float3(12f, 0f, 0f));
            _world.MoveTo(ship, new float3(0f, 0f, 15f));

            var widest = 0f;
            for (var t = 0f; t < 12f; t += TestWorld.FrameTime)
            {
                _world.Tick();
                var grid = _world.Grid();
                var tankAt = _world.PositionOf(tank);
                var shipAt = _world.PositionOf(ship);
                Assert.IsTrue(grid.IsWalkable(tankAt, NavLayer.Ground), $"tank entered water at {tankAt}");
                Assert.IsTrue(grid.IsWalkable(shipAt, NavLayer.Naval), $"ship left the water at {shipAt}");
                Assert.AreEqual(WaterLevel, shipAt.y, 1e-4f, "ships ride at the water level");
                widest = math.max(widest, math.abs(tankAt.z));
            }

            Assert.Less(math.distance(_world.PositionOf(tank).xz, new float2(12f, 0f)), 0.5f);
            Assert.Less(math.distance(_world.PositionOf(ship).xz, new float2(0f, 15f)), 0.5f);
            Assert.Greater(widest, 20f, "the tank went around the lake");
        }

        [Test]
        public void ShipOrderedOntoLand_StopsAtTheShore()
        {
            _world.SpawnNavArea(float3.zero, new float2(10f, 40f), NavAreaKind.Water);
            var ship = _world.SpawnShip(1, float3.zero);

            _world.MoveTo(ship, new float3(20f, 0f, 0f));
            _world.Run(5f);

            var position = _world.PositionOf(ship);
            Assert.IsTrue(_world.Grid().IsWalkable(position, NavLayer.Naval));
            Assert.Greater(position.x, 3.5f, "reached the shore nearest the goal");
        }

        [Test]
        public void Ships_PassUnderADeck_WithoutPushingUnitsOnIt()
        {
            _world.SpawnNavArea(float3.zero, new float2(8f, 200f), NavAreaKind.Water);
            _world.SpawnNavArea(new float3(0f, 3f, 0f), new float2(14f, 4f), NavAreaKind.Deck);
            var tank = _world.SpawnUnit(1, float3.zero, radius: 1f);
            var ship = _world.SpawnShip(1, new float3(0f, 0f, -15f), radius: 1f);

            _world.MoveTo(ship, new float3(0f, 0f, 15f));
            _world.Run(6f);

            var shipAt = _world.PositionOf(ship);
            var tankAt = _world.PositionOf(tank);
            Assert.Less(math.distance(shipAt.xz, new float2(0f, 15f)), 0.5f, "passed under the deck");
            Assert.AreEqual(WaterLevel, shipAt.y, 1e-4f);
            Assert.AreEqual(3f, tankAt.y, 1e-4f, "ground units stand on the deck");
            Assert.Less(math.length(tankAt.xz), 0.01f, "the ship never shoved the tank");
        }

        [Test]
        public void DestroyedBridge_ReroutesUnits_ToTheNextCrossing()
        {
            _world.SpawnNavArea(float3.zero, new float2(6f, 200f), NavAreaKind.Water);
            var near = _world.SpawnNavArea(new float3(0f, 2f, 0f), new float2(10f, 4f), NavAreaKind.Deck);
            _world.SpawnNavArea(new float3(0f, 2f, 30f), new float2(10f, 4f), NavAreaKind.Deck);
            var tank = _world.SpawnUnit(1, new float3(-12f, 0f, 0f), speed: 10f);
            var goal = new float3(12f, 0f, 0f);

            _world.MoveTo(tank, goal);
            _world.Tick();
            var version = _world.Grid().Version;
            Assert.AreEqual(1, _world.EntityManager.GetBuffer<PathWaypoint>(tank).Length, "straight over the near bridge");

            _world.EntityManager.DestroyEntity(near);
            var farthest = 0f;
            var onDeck = false;
            for (var t = 0f; t < 12f; t += TestWorld.FrameTime)
            {
                _world.Tick();
                var position = _world.PositionOf(tank);
                Assert.IsTrue(_world.Grid().IsWalkable(position), $"entered the river at {position}");
                farthest = math.max(farthest, position.z);
                if (math.abs(position.x) < 2f)
                {
                    Assert.AreEqual(2f, position.y, 1e-4f, "walks at deck height over the river");
                    onDeck = true;
                }
            }

            Assert.Greater(_world.Grid().Version, version, "the grid restamped without the bridge");
            Assert.Greater(farthest, 27f, "crossed at the far bridge");
            Assert.IsTrue(onDeck);
            Assert.Less(math.distance(_world.PositionOf(tank).xz, goal.xz), 0.5f);
        }

        [Test]
        public void ShorelinePlacement_NeedsLandAndWaterUnderTheFootprint()
        {
            // Sea east of x = 0.
            _world.SpawnNavArea(new float3(50f, 0f, 0f), new float2(100f, 200f), NavAreaKind.Water);
            _world.Tick();
            var map = Map();
            var grid = _world.Grid();
            var footprint = new float2(4f, 4f);
            var shore = float3.zero;
            var inland = new float3(-10f, 0f, 0f);
            var offshore = new float3(10f, 0f, 0f);

            Assert.IsTrue(PlacementMath.IsValid(map, grid, shore, footprint, PlacementSurface.Shoreline));
            Assert.IsFalse(PlacementMath.IsValid(map, grid, inland, footprint, PlacementSurface.Shoreline));
            Assert.IsFalse(PlacementMath.IsValid(map, grid, offshore, footprint, PlacementSurface.Shoreline));
            Assert.IsTrue(PlacementMath.IsValid(map, grid, inland, footprint));
            Assert.IsFalse(PlacementMath.IsValid(map, grid, shore, footprint), "land buildings stay off water");
            Assert.IsTrue(PlacementMath.IsValid(map, grid, offshore, footprint, PlacementSurface.Water));
            Assert.IsFalse(PlacementMath.IsValid(map, grid, shore, footprint, PlacementSurface.Water));
        }

        [Test]
        public void PlaceBuilding_AcceptsAShorelineSiteOnlyOnTheShore()
        {
            _world.SpawnNavArea(new float3(50f, 0f, 0f), new float2(100f, 200f), NavAreaKind.Water);
            var prefab = _world.MakePrefab(_world.SpawnBuilding(0, new float3(-90f, 0f, 90f), new float2(4f, 4f)));
            _world.EntityManager.AddComponentData(prefab, new BuildingPlacement { Surface = PlacementSurface.Shoreline });
            var builder = _world.MakeBuilder(_world.SpawnUnit(1, new float3(-6f, 0f, 6f)), prefab);

            Place(builder, prefab, new float3(-10f, 0f, 0f));
            _world.Tick();
            Assert.AreEqual(0, _world.All<BuildingTag>().Length, "inland is rejected");

            Place(builder, prefab, new float3(0.2f, 0f, 0.3f));
            _world.Tick();
            var sites = _world.All<BuildingTag>();
            Assert.AreEqual(1, sites.Length);
            Assert.AreEqual(new float2(0f, 0f), _world.Get<LocalTransform>(sites[0]).Position.xz);
        }

        private void Place(Entity builder, Entity prefab, float3 position) =>
            _world.Command(1, new PlayerCommand
            {
                Type = CommandType.PlaceBuilding, Unit = builder, Prefab = prefab, Position = position,
            });
    }
}
