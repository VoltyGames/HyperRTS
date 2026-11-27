using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Authoring;
using HyperRTS.Simulation.Vision;
using HyperRTS.Simulation.Vision.Fog;
using HyperRTS.Simulation.Vision.Sight;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Tests;

/// <summary>Fog-of-war helpers on top of <see cref="TestWorld"/>.</summary>
public static class VisionTestKit
{
    extension(TestWorld world)
    {
        public FogOfWar Fog()
        {
            world.EntityManager.CompleteAllTrackedJobs();
            return world.Singleton<FogOfWar>();
        }
    }

    extension(TestWorld world)
    {
        /// <summary>A solid box sight can't pass below its top, as a house or a wall bakes.</summary>
        public Entity SpawnSightBlock(float3 position, float2 size, float height) =>
            world.SpawnSightBlock(position, new SightBlock { Size = size, Height = height });
    }

    extension(TestWorld world)
    {
        public Entity SpawnSightBlock(float3 position, in SightBlock block)
        {
            var entity = world.EntityManager.CreateEntity();
            world.Add(entity, LocalTransform.FromPosition(position));
            var writer = world.Writer(entity);
            SightBlockAuthoring.Add(writer, block);
            return entity;
        }
    }

    extension(TestWorld world)
    {
        /// <summary>A unit of <paramref name="owner"/> that sees <paramref name="range"/> metres from an eye <paramref name="eye"/> high.</summary>
        public Entity SpawnViewer(byte owner, float3 position, float range, float eye = 2f)
        {
            var viewer = world.SpawnUnit(owner, position);
            world.Set(viewer, new VisionRange { Value = range, EyeHeight = eye });
            return viewer;
        }
    }

    extension(TestWorld world)
    {
        public bool IsFogHidden(Entity entity) =>
            world.EntityManager.HasComponent<FogHidden>(entity);
    }

    extension(TestWorld world)
    {
        public void DisableFog() => world.SetFog(false);
    }

    extension(TestWorld world)
    {
        public void SetFog(bool on)
        {
            using var query = world.Query(typeof(MapSettings));
            var settings = query.GetSingleton<MapSettings>();
            settings.FogOfWar = on;
            query.SetSingleton(settings);
        }
    }
}
