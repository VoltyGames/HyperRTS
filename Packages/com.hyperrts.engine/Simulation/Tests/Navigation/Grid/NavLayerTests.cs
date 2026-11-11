using HyperRTS.Simulation.Buildings.Placement;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Geometry;
using HyperRTS.Simulation.Navigation.Agents;
using HyperRTS.Simulation.Navigation.Areas;
using HyperRTS.Simulation.Navigation.Grid;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Tests.Navigation.Grid
{
    /// <summary>
    /// Nav layers: ground and naval over land and water, shoreline placement, bridge decks that can be destroyed,
    /// and aircraft flying straight over all of it.
    /// </summary>
    public class NavLayerTests : SimulationFixture
    {
        private const float WaterLevel = -1f;

        [SetUp]
        public void Prepare()
        {
            World.ConfigureMap(WaterLevel);
        }

        private MapSettings Map()
        {
            using var query = World.Query(typeof(MapSettings));
            return query.GetSingleton<MapSettings>();
        }

        [Test]
        public void GroundUnits_RouteAroundWater_WhileShipsCrossIt()
        {
            World.SpawnNavArea(float3.zero, new float2(10f, 40f), NavAreaKind.Water);
            var tank = World.SpawnUnit(1, new float3(-12f, 0f, 0f), new TestUnit { Speed = 10f });
            var ship = World.SpawnShip(1, new float3(0f, 0f, -15f));
            World.MoveTo(tank, new float3(12f, 0f, 0f));
            World.MoveTo(ship, new float3(0f, 0f, 15f));

            var widest = 0f;
            for (var t = 0f; t < 12f; t += TestWorld.FrameTime)
            {
                World.Tick();
                var grid = World.Grid();
                var tankAt = World.GetPosition(tank);
                var shipAt = World.GetPosition(ship);
                Assert.IsTrue(grid.IsWalkable(tankAt, NavLayer.Ground), $"tank entered water at {tankAt}");
                Assert.IsTrue(grid.IsWalkable(shipAt, NavLayer.Naval), $"ship left the water at {shipAt}");
                Assert.AreEqual(WaterLevel, shipAt.y, 1e-4f, "ships ride at the water level");
                widest = math.max(widest, math.abs(tankAt.z));
            }

            Assert.Less(math.distance(World.GetPosition(tank).xz, new float2(12f, 0f)), 0.5f);
            Assert.Less(math.distance(World.GetPosition(ship).xz, new float2(0f, 15f)), 0.5f);
            Assert.Greater(widest, 20f, "the tank went around the lake");
        }

        [Test]
        public void ShipOrderedOntoLand_StopsAtTheShore()
        {
            World.SpawnNavArea(float3.zero, new float2(10f, 40f), NavAreaKind.Water);
            var ship = World.SpawnShip(1, float3.zero);

            World.MoveTo(ship, new float3(20f, 0f, 0f));
            World.Run(5f);

            var position = World.GetPosition(ship);
            Assert.IsTrue(World.Grid().IsWalkable(position, NavLayer.Naval));
            Assert.Greater(position.x, 3.5f, "reached the shore nearest the goal");
        }

        [Test]
        public void Ships_PassUnderADeck_WithoutPushingUnitsOnIt()
        {
            World.SpawnNavArea(float3.zero, new float2(8f, 200f), NavAreaKind.Water);
            World.SpawnNavArea(new float3(0f, 3f, 0f), new float2(14f, 4f), NavAreaKind.Deck);
            var tank = World.SpawnUnit(1, float3.zero, new TestUnit { Radius = 1f });
            var ship = World.SpawnShip(1, new float3(0f, 0f, -15f), radius: 1f);

            World.MoveTo(ship, new float3(0f, 0f, 15f));
            World.Run(6f);

            var shipAt = World.GetPosition(ship);
            var tankAt = World.GetPosition(tank);
            Assert.Less(math.distance(shipAt.xz, new float2(0f, 15f)), 0.5f, "passed under the deck");
            Assert.AreEqual(WaterLevel, shipAt.y, 1e-4f);
            Assert.AreEqual(3f, tankAt.y, 1e-4f, "ground units stand on the deck");
            Assert.Less(math.length(tankAt.xz), 0.01f, "the ship never shoved the tank");
        }

        [Test]
        public void DestroyedBridge_ReroutesUnits_ToTheNextCrossing()
        {
            World.SpawnNavArea(float3.zero, new float2(6f, 200f), NavAreaKind.Water);
            var near = World.SpawnNavArea(new float3(0f, 2f, 0f), new float2(10f, 4f), NavAreaKind.Deck);
            World.SpawnNavArea(new float3(0f, 2f, 30f), new float2(10f, 4f), NavAreaKind.Deck);
            var tank = World.SpawnUnit(1, new float3(-12f, 0f, 0f), new TestUnit { Speed = 10f });
            var goal = new float3(12f, 0f, 0f);

            World.MoveTo(tank, goal);
            World.Tick();
            var version = World.Grid().Version;
            Assert.AreEqual(1, World.Buffer<PathWaypoint>(tank).Length, "straight over the near bridge");

            World.EntityManager.DestroyEntity(near);
            var farthest = 0f;
            var onDeck = false;
            for (var t = 0f; t < 12f; t += TestWorld.FrameTime)
            {
                World.Tick();
                var position = World.GetPosition(tank);
                Assert.IsTrue(World.Grid().IsWalkable(position), $"entered the river at {position}");
                farthest = math.max(farthest, position.z);
                if (math.abs(position.x) < 2f)
                {
                    Assert.AreEqual(2f, position.y, 1e-4f, "walks at deck height over the river");
                    onDeck = true;
                }
            }

            Assert.Greater(World.Grid().Version, version, "the grid restamped without the bridge");
            Assert.Greater(farthest, 27f, "crossed at the far bridge");
            Assert.IsTrue(onDeck);
            Assert.Less(math.distance(World.GetPosition(tank).xz, goal.xz), 0.5f);
        }

        [Test]
        public void ShorelinePlacement_NeedsLandAndWaterUnderTheFootprint()
        {
            // Sea east of x = 0.
            World.SpawnNavArea(new float3(50f, 0f, 0f), new float2(100f, 200f), NavAreaKind.Water);
            World.Tick();
            var map = Map();
            var grid = World.Grid();
            var footprint = new float2(4f, 4f);
            var shore = float3.zero;
            var inland = new float3(-10f, 0f, 0f);
            var offshore = new float3(10f, 0f, 0f);
            bool Fits(float3 center, PlacementSurface surface) =>
                new BuildingSpot(center, PlacementMath.CameraYaw, footprint, surface).IsValid(map, grid);

            Assert.IsTrue(Fits(shore, PlacementSurface.Shoreline));
            Assert.IsFalse(Fits(inland, PlacementSurface.Shoreline));
            Assert.IsFalse(Fits(offshore, PlacementSurface.Shoreline));
            Assert.IsTrue(Fits(inland, PlacementSurface.Land));
            Assert.IsFalse(Fits(shore, PlacementSurface.Land), "land buildings stay off water");
            Assert.IsTrue(Fits(offshore, PlacementSurface.Water));
            Assert.IsFalse(Fits(shore, PlacementSurface.Water));
        }

        [Test]
        public void Aircraft_FlyStraightOverWaterAndObstacles_AtAltitude()
        {
            World.SpawnNavArea(float3.zero, new float2(10f, 40f), NavAreaKind.Water);
            World.SpawnNavArea(new float3(10f, 0f, 0f), new float2(4f, 40f), NavAreaKind.Blocked);
            var aircraft = World.SpawnAircraft(1, new float3(-15f, 0f, 0f), altitude: 10f);
            World.MoveTo(aircraft, new float3(20f, 0f, 0f));

            for (var t = 0f; t < 6f; t += TestWorld.FrameTime)
            {
                World.Tick();
                Assert.AreEqual(0f, World.GetPosition(aircraft).z, 1e-3f, "flew a straight line");
            }

            var position = World.GetPosition(aircraft);
            Assert.Less(math.distance(position.xz, new float2(20f, 0f)), 0.5f);
            Assert.AreEqual(10f, position.y, 1e-3f, "cruises at its altitude over the ground");
        }

        [Test]
        public void IdleHelicopter_Hovers_WhileIdleJet_KeepsCircling()
        {
            var helicopter = World.SpawnAircraft(1, new float3(-10f, 0f, 0f));
            var jet = World.SpawnAircraft(1, new float3(10f, 0f, 0f), loiterRadius: 5f);
            World.Run(2f);

            var hover = World.GetPosition(helicopter);
            var before = World.GetPosition(jet);
            World.Run(1f);

            Assert.AreEqual(hover, World.GetPosition(helicopter), "a helicopter holds still");
            var after = World.GetPosition(jet);
            Assert.Greater(math.distance(before.xz, after.xz), 1f, "a jet keeps flying");
            Assert.Less(math.distance(after.xz, new float2(10f, 0f)), 10.5f, "within its loiter circle");
        }

        [Test]
        public void Aircraft_OnlySeparateFromAircraft()
        {
            var tank = World.SpawnUnit(1, float3.zero);
            var helicopter = World.SpawnAircraft(1, float3.zero);
            var other = World.SpawnAircraft(1, new float3(0.2f, 0f, 0f));
            World.Run(1f);

            Assert.AreEqual(0f, math.length(World.GetPosition(tank).xz), 1e-4f, "the tank under them isn't pushed");
            var gap = math.distance(World.GetPosition(helicopter).xz, World.GetPosition(other).xz);
            Assert.GreaterOrEqual(gap, 0.99f, "aircraft spread apart");
        }
    }
}
