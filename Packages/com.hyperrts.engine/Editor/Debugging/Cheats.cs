using HyperRTS.Simulation.Buildings;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Match;
using HyperRTS.Simulation.Navigation;
using HyperRTS.Simulation.Resources;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEditor;
using UnityEngine;

namespace HyperRTS.Editor.Debugging
{
    /// <summary>Play-mode cheats applied straight to the running world; editor only, never part of the game.</summary>
    internal static class Cheats
    {
        // The game speed slider must not leak out of Play mode.
        [InitializeOnLoadMethod]
        private static void ResetSpeedAfterPlay() => EditorApplication.playModeStateChanged += change =>
        {
            if (change == PlayModeStateChange.EnteredEditMode)
            {
                Time.timeScale = 1f;
            }
        };

        public static NativeArray<Entity> Players(EntityManager entityManager)
        {
            using var query = entityManager.CreateEntityQuery(typeof(Player));
            return query.ToEntityArray(Allocator.Temp);
        }

        /// <summary>Entity prefabs that can be spawned: everything producers, builders and death spawns reference.</summary>
        public static NativeArray<Entity> Prefabs(EntityManager entityManager)
        {
            using var query = new EntityQueryBuilder(Allocator.Temp).WithAll<EntityInfo, Prefab>()
                .WithOptions(EntityQueryOptions.IncludePrefab).Build(entityManager);
            return query.ToEntityArray(Allocator.Temp);
        }

        /// <summary>Hands control of a player to you; its AI stops so the two don't fight over it.</summary>
        public static void MakeLocal(EntityManager entityManager, Entity player)
        {
            foreach (var other in Players(entityManager))
            {
                entityManager.RemoveComponent<LocalPlayer>(other);
            }

            entityManager.AddComponent<LocalPlayer>(player);
            entityManager.RemoveComponent<AIPlayer>(player);
        }

        public static void AddResources(EntityManager entityManager, Entity player, int amount)
        {
            var stock = entityManager.GetBuffer<ResourceStock>(player);
            foreach (var type in EditorAssets.FindAssets<ResourceType>())
            {
                ResourceMath.Add(stock, type, amount);
            }
        }

        public static bool FogEnabled(EntityManager entityManager) =>
            PlayWorld.TryGetSingleton(entityManager, out MapSettings map) && map.FogOfWar;

        public static void SetFog(EntityManager entityManager, bool enabled)
        {
            using var query = entityManager.CreateEntityQuery(typeof(MapSettings));
            var map = query.GetSingleton<MapSettings>();
            map.FogOfWar = enabled;
            query.SetSingleton(map);
        }

        /// <summary>Finishes every construction site and the current production of one faction.</summary>
        public static void InstantBuild(EntityManager entityManager, byte faction)
        {
            using var sites = entityManager.CreateEntityQuery(typeof(ConstructionProgress), typeof(Faction));
            foreach (var site in sites.ToEntityArray(Allocator.Temp))
            {
                if (entityManager.GetComponentData<Faction>(site).Value == faction)
                {
                    entityManager.SetComponentData(site, new ConstructionProgress { Value = 1f });
                    entityManager.SetComponentEnabled<ConstructionProgress>(site, false);
                }
            }

            using var producers = entityManager.CreateEntityQuery(typeof(Producer), typeof(ProductionQueueItem), typeof(Faction));
            foreach (var entity in producers.ToEntityArray(Allocator.Temp))
            {
                if (entityManager.GetComponentData<Faction>(entity).Value == faction &&
                    entityManager.GetBuffer<ProductionQueueItem>(entity, true).Length > 0)
                {
                    var producer = entityManager.GetComponentData<Producer>(entity);
                    producer.Elapsed = float.MaxValue;
                    entityManager.SetComponentData(entity, producer);
                }
            }
        }

        public static void Spawn(EntityManager entityManager, Entity prefab, byte faction, float3 position)
        {
            var entity = entityManager.Instantiate(prefab);
            var transform = entityManager.GetComponentData<LocalTransform>(prefab);
            transform.Position = position;
            entityManager.SetComponentData(entity, transform);
            entityManager.SetComponentData(entity, new Faction { Value = faction });
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

            if (!PlayWorld.TryGetSingleton(entityManager, out MapSettings map))
            {
                return new float3(0f, terrain.Height(float2.zero), 0f);
            }

            var center = map.Min + map.Size * 0.5f;
            return new float3(center.x, terrain.Height(center), center.y);
        }
    }
}
