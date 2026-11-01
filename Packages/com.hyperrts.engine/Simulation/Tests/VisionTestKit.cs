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
        public static Entity SpawnSightBlock(this TestWorld world, float3 position, float2 size, float height)
        {
            var block = world.EntityManager.CreateEntity();
            world.EntityManager.AddComponentData(block, LocalTransform.FromPosition(position));
            var writer = new EntityManagerWriter(world.EntityManager, block);
            SightBlockAuthoring.Add(ref writer, size, height);
            return block;
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
