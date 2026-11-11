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

namespace HyperRTS.Editor.PlayMode.Cheats
{
    /// <summary>Spawns any spawnable prefab where the camera looks; buildings obey placement as a player's do.</summary>
    internal static class SpawnCheat
    {
        // Spawned groups stand on a square grid this far apart, so they don't start inside each other.
        private const float Spacing = 6f;

        /// <summary>Entity prefabs that can be spawned: everything producers, builders and death spawns reference.</summary>
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
            PlayWorld.TryGetSingleton(entityManager, out TerrainHeight terrain);
            var center = ViewCenter(entityManager, terrain);
            var building = entityManager.HasComponent<BuildingTag>(prefab) && entityManager.HasComponent<NavObstacle>(prefab);
            var placement = building ? new Placement(entityManager, prefab) : default;
            var rotation = entityManager.GetComponentData<LocalTransform>(prefab).Rotation;
            var side = (int)math.ceil(math.sqrt(count));
            var blocked = 0;
            for (var i = 0; i < count; i++)
            {
                var spot = center.xz + (new float2(i % side, i / side) - (side - 1) * 0.5f) * Spacing;
                var position = new float3(spot.x, terrain.Height(spot), spot.y);
                if (!building)
                {
                    entityManager.Spawn(prefab, position, rotation, owner);
                }
                else if (!placement.TrySpawn(entityManager, prefab, owner, position))
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

        // Where the game camera looks on the ground, or the map centre without a camera.
        private static float3 ViewCenter(EntityManager entityManager, in TerrainHeight terrain)
        {
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

        /// <summary>The placement rule a player's buildings follow, with the sites spawned so far.</summary>
        private readonly struct Placement
        {
            private readonly MapSettings _map;
            private readonly NavGrid _grid;
            private readonly float2 _footprint;
            private readonly PlacementSurface _surface;
            private readonly NativeList<PlacedFootprint> _sites;

            public Placement(EntityManager entityManager, Entity prefab)
            {
                PlayWorld.TryGetSingleton(entityManager, out _map);
                PlayWorld.TryGetSingleton(entityManager, out _grid);
                _footprint = entityManager.GetComponentData<NavObstacle>(prefab).Size;
                _surface = BuildingPlacement.GetSurface(entityManager, prefab);
                using var obstacles = new EntityQueryBuilder(Allocator.Temp).WithAll<NavObstacle, LocalTransform>()
                    .WithNone<Dead>().Build(entityManager);
                _sites = PlacedFootprint.Read(entityManager, obstacles);
            }

            // Skips a blocked spot. The building rises as a finished site, so SiteClearingSystem removes the open
            // scenery under it before ConstructionSystem completes it.
            public bool TrySpawn(EntityManager entityManager, Entity prefab, byte owner, float3 position)
            {
                var spot = new BuildingSpot(position, 0f, _footprint, _surface).Snap(_map).Grounded(_grid);
                var inLine = entityManager.HasComponent<LinePlacement>(prefab);
                if (!spot.CanPlace(_map, _grid, _sites.AsArray(), inLine))
                {
                    return false;
                }

                var facing = math.mul(spot.Rotation, entityManager.GetComponentData<LocalTransform>(prefab).Rotation);
                var building = entityManager.Spawn(prefab, spot.Center, facing, owner);
                entityManager.SetComponentData(building, new ConstructionProgress { Value = 1f });
                entityManager.SetComponentEnabled<ConstructionProgress>(building, true);
                _sites.Add(new PlacedFootprint(spot.Box, inLine));
                return true;
            }
        }
    }
}
