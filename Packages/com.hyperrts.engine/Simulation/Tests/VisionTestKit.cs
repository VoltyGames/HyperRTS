using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Authoring;
using HyperRTS.Simulation.Vision;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Tests
{
    /// <summary>Fog-of-war helpers on top of <see cref="TestWorld"/>.</summary>
    public static class VisionTestKit
    {
        public static FogOfWar Fog(this TestWorld world)
        {
            world.EntityManager.CompleteAllTrackedJobs();
            return world.Singleton<FogOfWar>();
        }

        /// <summary>A solid box sight can't pass below its top, as a house or a wall bakes.</summary>
        public static Entity SpawnSightBlock(this TestWorld world, float3 position, float2 size, float height) =>
            world.SpawnSightBlock(position, new SightBlock { Size = size, Height = height });

        public static Entity SpawnSightBlock(this TestWorld world, float3 position, in SightBlock block)
        {
            var entity = world.EntityManager.CreateEntity();
            world.EntityManager.AddComponentData(entity, LocalTransform.FromPosition(position));
            var writer = new EntityManagerWriter(world.EntityManager, entity);
            SightBlockAuthoring.Add(ref writer, block);
            return entity;
        }

        public static void DisableFog(this TestWorld world)
        {
            using var query = world.EntityManager.CreateEntityQuery(typeof(MapSettings));
            var settings = query.GetSingleton<MapSettings>();
            settings.FogOfWar = false;
            query.SetSingleton(settings);
        }
    }
}
