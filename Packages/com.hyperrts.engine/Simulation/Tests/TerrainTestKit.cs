using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Navigation;
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
            using var query = world.EntityManager.CreateEntityQuery(typeof(MapSettings));
            var map = query.GetSingleton<MapSettings>();
            map.WaterLevel = waterLevel;
            map.MaxSlope = maxSlope;
            map.FloodTerrain = floodTerrain;
            query.SetSingleton(map);
        }

        public static Entity SpawnNavArea(this TestWorld world, float3 position, float2 size, NavAreaKind kind)
        {
            var area = world.EntityManager.CreateEntity();
            world.EntityManager.AddComponentData(area, LocalTransform.FromPosition(position));
            var sink = new EntityManagerSink(world.EntityManager, area);
            NavSetup.AddArea(ref sink, size, kind);
            return area;
        }

        public static Entity SpawnShip(this TestWorld world, byte faction, float3 position, float radius = 0.5f)
        {
            var ship = world.SpawnUnit(faction, position, speed: 8f, radius: radius, name: "Ship");
            world.EntityManager.SetComponentData(ship, new NavAgent { Radius = radius, Layer = NavLayer.Naval });
            return ship;
        }

        public static void MoveTo(this TestWorld world, Entity unit, float3 goal) =>
            world.Command(world.Get<Faction>(unit).Value,
                new PlayerCommand { Type = CommandType.Move, Unit = unit, Position = goal });

        public static NavGrid Grid(this TestWorld world)
        {
            world.EntityManager.CompleteAllTrackedJobs();
            using var query = world.EntityManager.CreateEntityQuery(typeof(NavGrid));
            return query.GetSingleton<NavGrid>();
        }

        public static float3 PositionOf(this TestWorld world, Entity entity) =>
            world.Get<LocalTransform>(entity).Position;
    }
}
