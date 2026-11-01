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
            var writer = new EntityWriter(world.EntityManager, entity);
            SightBlockAuthoring.Add(writer, block);
            return entity;
        }

        /// <summary>A unit of <paramref name="faction"/> that sees <paramref name="range"/> metres from an eye <paramref name="eye"/> high.</summary>
        public static Entity SpawnViewer(this TestWorld world, byte faction, float3 position, float range, float eye = 2f)
        {
            var viewer = world.SpawnUnit(faction, position);
            world.EntityManager.SetComponentData(viewer, new VisionRange { Value = range, EyeHeight = eye });
            return viewer;
        }

        public static bool IsFogHidden(this TestWorld world, Entity entity) =>
            world.EntityManager.HasComponent<FogHidden>(entity);

        public static void DisableFog(this TestWorld world) => world.SetFog(false);

        public static void SetFog(this TestWorld world, bool on)
        {
            using var query = world.EntityManager.CreateEntityQuery(typeof(MapSettings));
            var settings = query.GetSingleton<MapSettings>();
            settings.FogOfWar = on;
            query.SetSingleton(settings);
        }
    }
}
