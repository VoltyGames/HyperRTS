using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Authoring;
using HyperRTS.Simulation.Common.Players;
using HyperRTS.Simulation.Navigation.Agents;
using HyperRTS.Simulation.Navigation.Areas;
using HyperRTS.Simulation.Navigation.Grid;
using HyperRTS.Simulation.Orders;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Tests
{
    /// <summary>Terrain, water and nav layer helpers on top of <see cref="TestWorld"/>.</summary>
    public static class TerrainTestKit
    {
        /// <summary>Changing these after the first tick rebuilds the nav grid.</summary>
        public static void ConfigureMap(this TestWorld world, float waterLevel = 0f, float maxSlope = 0f,
            bool floodTerrain = false)
        {
            using var query = world.Query(typeof(MapSettings));
            var map = query.GetSingleton<MapSettings>();
            map.WaterLevel = waterLevel;
            map.MaxSlope = maxSlope;
            map.FloodTerrain = floodTerrain;
            query.SetSingleton(map);
        }

        public static Entity SpawnNavArea(this TestWorld world, float3 position, float2 size, NavAreaKind kind)
        {
            var area = world.EntityManager.CreateEntity();
            world.Add(area, LocalTransform.FromPosition(position));
            var writer = world.Writer(area);
            NavSetup.AddArea(writer, size, kind);
            return area;
        }

        /// <summary>A 12 m river across the map, crossed by one 6 m deck 20 m long: a one-lane bridge on the x axis.</summary>
        public static void SpawnBridge(this TestWorld world)
        {
            world.SpawnNavArea(float3.zero, new float2(12f, 600f), NavAreaKind.Water);
            world.SpawnNavArea(float3.zero, new float2(20f, 6f), NavAreaKind.Deck);
        }

        public static Entity SpawnObstacle(this TestWorld world, float3 center, float2 size, float yaw = 0f)
        {
            var obstacle = world.EntityManager.CreateEntity();
            world.Add(obstacle,
                LocalTransform.FromPositionRotation(center, quaternion.RotateY(yaw)));
            var writer = world.Writer(obstacle);
            NavSetup.AddObstacle(writer, size);
            return obstacle;
        }

        public static Entity SpawnShip(this TestWorld world, byte owner, float3 position, float radius = 0.5f)
        {
            var ship = world.SpawnUnit(owner, position, speed: 8f, radius: radius, name: "Ship");
            world.Set(ship, new NavAgent { Radius = radius, Layer = NavLayer.Naval });
            return ship;
        }

        public static void MoveTo(this TestWorld world, Entity unit, float3 goal) =>
            world.Command(world.Get<Owner>(unit).Value,
                new PlayerCommand { Type = CommandType.Move, Unit = unit, Position = goal });

        public static NavGrid Grid(this TestWorld world)
        {
            world.EntityManager.CompleteAllTrackedJobs();
            using var query = world.Query(typeof(NavGrid));
            return query.GetSingleton<NavGrid>();
        }

        /// <summary>The agent's view of the grid, its movement class registered first.</summary>
        public static NavAccess Access(this TestWorld world, in NavAgent agent)
        {
            world.AddAgentPrefabs(agent);
            world.Tick();
            return agent.Access(world.Grid());
        }

        public static float3 GetPosition(this TestWorld world, Entity entity) =>
            world.Get<LocalTransform>(entity).Position;
    }
}
