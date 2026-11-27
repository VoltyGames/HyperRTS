using HyperRTS.Simulation.Buildings.Placement;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Buildings;
using HyperRTS.Simulation.Common.Prefabs;
using HyperRTS.Simulation.Navigation.Areas;
using HyperRTS.Simulation.Navigation.Grid;
using HyperRTS.Simulation.Navigation.Heights;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace HyperRTS.Editor.PlayMode.Cheats;

/// <summary>Spawns any spawnable prefab where the camera looks; buildings obey placement as a player's do.</summary>
internal static class SpawnCheat
{
    // Spawned groups stand on a square grid this far apart, so they don't start inside each other.
    private const float Spacing = 6f;

    /// <summary>Entity prefabs that can be spawned: everything producers, builders and remains reference.</summary>
    public static NativeArray<Entity> Prefabs(EntityManager entityManager)
    {
        using var query = new EntityQueryBuilder(Allocator.Temp).WithAll<EntityInfo, Prefab>()
            .WithOptions(EntityQueryOptions.IncludePrefab).Build(entityManager);
        return query.ToEntityArray(Allocator.Temp);
    }

    /// <summary>The spawnable prefab with <paramref name="typeId"/>, or <c>Entity.Null</c>.</summary>
    public static Entity FindPrefab(EntityManager entityManager, int typeId) =>
        PlayWorld.TryGetSingleton(entityManager, out PrefabRegistry registry) ? registry.Find(typeId) : Entity.Null;

    /// <summary>Spawns <paramref name="count"/> of <paramref name="prefab"/> on a grid where the camera looks, facing as the prefab does.</summary>
    public static void Spawn(EntityManager entityManager, Entity prefab, byte owner, int count)
    {
        var center = ViewCenter(entityManager);
        var side = (int)math.ceil(math.sqrt(count));
        var blocked = 0;
        for (var i = 0; i < count; i++)
        {
            var spot = center.xz + (new float2(i % side, i / side) - (side - 1) * 0.5f) * Spacing;
            if (SpawnAt(entityManager, prefab, owner, spot, 0f) == Entity.Null)
            {
                blocked++;
            }
        }

        if (blocked > 0)
        {
            var name = entityManager.GetComponentData<EntityInfo>(prefab).Name;
            Debug.LogWarning($"[HyperRTS] {blocked} of {count} {name} not spawned: the ground there is blocked.");
        }
    }

    /// <summary>
    /// Spawns one <paramref name="prefab"/> at a map point, turned <paramref name="yaw"/> radians about Y from its
    /// own facing; <c>Entity.Null</c> when a building's ground is blocked.
    /// </summary>
    public static Entity SpawnAt(EntityManager entityManager, Entity prefab, byte owner, float2 spot, float yaw)
    {
        PlayWorld.TryGetSingleton(entityManager, out TerrainHeight terrain);
        var position = new float3(spot.x, terrain.Height(spot), spot.y);
        var building = entityManager.HasComponent<BuildingTag>(prefab) && entityManager.HasComponent<NavObstacle>(prefab);
        if (building)
        {
            return TryPlace(entityManager, prefab, owner, position, yaw);
        }

        var rotation = math.mul(quaternion.RotateY(yaw), entityManager.GetComponentData<LocalTransform>(prefab).Rotation);
        return entityManager.Spawn(prefab, position, rotation, owner);
    }

    /// <summary>Where the game camera looks on the ground, or the map centre without a camera.</summary>
    public static float3 ViewCenter(EntityManager entityManager)
    {
        PlayWorld.TryGetSingleton(entityManager, out TerrainHeight terrain);
        var camera = Camera.main;
        if (camera != null)
        {
            var ray = camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            if (terrain.Raycast(ray.origin, ray.direction, camera.farClipPlane, out var point))
            {
                return point;
            }
        }

        PlayWorld.TryGetSingleton(entityManager, out MapSettings map);
        var center = map.Center.xz;
        return new float3(center.x, terrain.Height(center), center.y);
    }

    // The placement rule a player's buildings follow, over every building standing, so a batch never overlaps
    // itself. Null for a blocked spot. The building rises as a finished site, so SiteClearingSystem removes the
    // open scenery under it before ConstructionSystem completes it.
    private static Entity TryPlace(EntityManager entityManager, Entity prefab, byte owner, float3 position, float yaw)
    {
        PlayWorld.TryGetSingleton(entityManager, out MapSettings map);
        PlayWorld.TryGetSingleton(entityManager, out NavGrid grid);
        var footprint = entityManager.GetComponentData<NavObstacle>(prefab).Size;
        var surface = BuildingPlacement.GetSurface(entityManager, prefab);
        var spot = new BuildingSpot(position, yaw, footprint, surface).Snap(map).Grounded(grid);
        using var obstacles = new EntityQueryBuilder(Allocator.Temp).WithAll<NavObstacle, LocalTransform>()
            .Build(entityManager);
        using var sites = PlacedFootprint.Read(entityManager, obstacles);
        if (!spot.CanPlace(map, grid, sites.AsArray(), entityManager.HasComponent<LinePlacement>(prefab)))
        {
            return Entity.Null;
        }

        var facing = math.mul(spot.Rotation, entityManager.GetComponentData<LocalTransform>(prefab).Rotation);
        var building = entityManager.Spawn(prefab, spot.Center, facing, owner);
        entityManager.SetComponentData(building, new ConstructionProgress { Value = 1f });
        entityManager.SetComponentEnabled<ConstructionProgress>(building, true);
        return building;
    }
}
