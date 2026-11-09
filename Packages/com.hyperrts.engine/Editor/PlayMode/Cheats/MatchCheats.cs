using HyperRTS.Editor.Common;
using HyperRTS.Simulation.AI;
using HyperRTS.Simulation.Combat.Damage;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Players;
using HyperRTS.Simulation.Match;
using HyperRTS.Simulation.Navigation.Heights;
using HyperRTS.Simulation.Production;
using HyperRTS.Simulation.Resources;
using HyperRTS.Simulation.Selection;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEditor;
using UnityEngine;

namespace HyperRTS.Editor.PlayMode.Cheats
{
    /// <summary>Play-mode cheats applied straight to the authoritative world; editor only, never part of the game.</summary>
    internal static class MatchCheats
    {
        // Spawned groups stand on a square grid this far apart, so they don't start inside each other.
        private const float SpawnSpacing = 6f;

        // Larger than any health, so one untyped hit kills through armour.
        private const float LethalDamage = 1e9f;

        // The game speed slider must not leak out of Play mode.
        [InitializeOnLoadMethod]
        private static void ResetSpeedAfterPlay() => EditorApplication.playModeStateChanged += change =>
        {
            if (change == PlayModeStateChange.EnteredEditMode)
            {
                LocalGameSpeed.Reset();
            }
        };

        /// <summary>Entity prefabs that can be spawned: everything producers, builders and death spawns reference.</summary>
        public static NativeArray<Entity> Prefabs(EntityManager entityManager)
        {
            using var query = new EntityQueryBuilder(Allocator.Temp).WithAll<EntityInfo, Prefab>()
                .WithOptions(EntityQueryOptions.IncludePrefab).Build(entityManager);
            return query.ToEntityArray(Allocator.Temp);
        }

        /// <summary>Hands control of a player to you; its AI stops so the two don't fight over it. Single player only.</summary>
        public static void MakeLocal(EntityManager entityManager, Entity player)
        {
            foreach (var other in PlayWorld.Players(entityManager))
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
            if (!query.TryGetSingleton(out MapSettings map))
            {
                return;
            }

            map.FogOfWar = enabled;
            query.SetSingleton(map);
        }

        /// <summary>
        /// Finishes every construction site and the current production of one owner, through the construction and
        /// production systems next frame, so completion events, sounds and stats still happen.
        /// </summary>
        public static void InstantBuild(EntityManager entityManager, byte owner)
        {
            using var sites = entityManager.CreateEntityQuery(typeof(ConstructionProgress), typeof(Owner));
            foreach (var site in sites.ToEntityArray(Allocator.Temp))
            {
                if (entityManager.GetComponentData<Owner>(site).Value == owner)
                {
                    entityManager.SetComponentData(site, new ConstructionProgress { Value = 1f });
                }
            }

            using var producers = entityManager.CreateEntityQuery(typeof(Producer), typeof(ProductionQueueItem), typeof(Owner));
            foreach (var entity in producers.ToEntityArray(Allocator.Temp))
            {
                FinishProduction(entityManager, entity, owner);
            }
        }

        private static void FinishProduction(EntityManager entityManager, Entity entity, byte owner)
        {
            var owned = entityManager.GetComponentData<Owner>(entity).Value == owner;
            if (!owned || entityManager.GetBuffer<ProductionQueueItem>(entity, true).Length == 0)
            {
                return;
            }

            var producer = entityManager.GetComponentData<Producer>(entity);
            producer.Elapsed = float.MaxValue;
            entityManager.SetComponentData(entity, producer);
        }

        /// <summary>The spawnable prefab with <paramref name="typeId"/>, or <c>Entity.Null</c>.</summary>
        public static Entity FindPrefab(EntityManager entityManager, int typeId)
        {
            foreach (var prefab in Prefabs(entityManager))
            {
                if (entityManager.GetComponentData<EntityInfo>(prefab).TypeId == typeId)
                {
                    return prefab;
                }
            }

            return Entity.Null;
        }

        /// <summary>Spawns <paramref name="count"/> of <paramref name="prefab"/> on a grid round <paramref name="center"/>, on the ground and facing as the prefab does.</summary>
        public static void Spawn(EntityManager entityManager, Entity prefab, byte owner, float3 center, int count)
        {
            PlayWorld.TryGetSingleton(entityManager, out TerrainHeight terrain);
            var rotation = entityManager.GetComponentData<LocalTransform>(prefab).Rotation;
            var side = (int)math.ceil(math.sqrt(count));
            for (var i = 0; i < count; i++)
            {
                var offset = (new float2(i % side, i / side) - (side - 1) * 0.5f) * SpawnSpacing;
                var spot = center.xz + offset;
                entityManager.Spawn(prefab, new float3(spot.x, terrain.Height(spot), spot.y), rotation, owner);
            }
        }

        /// <summary>
        /// The local player's selected entities with health, or none when the selection lives in another world than
        /// the cheats write to (a client of a dedicated server).
        /// </summary>
        public static NativeArray<Entity> Selection(EntityManager entityManager)
        {
            var presented = PlayWorld.TryGetPresented(out var view) && view.World == entityManager.World;
            if (!presented)
            {
                return new NativeArray<Entity>(0, Allocator.Temp);
            }

            using var query = entityManager.CreateEntityQuery(typeof(Selected), typeof(Health));
            return query.ToEntityArray(Allocator.Temp);
        }

        /// <summary>Heals <paramref name="units"/> to full through the damage system.</summary>
        public static void Repair(EntityManager entityManager, NativeArray<Entity> units)
        {
            foreach (var unit in units)
            {
                Hit(entityManager, unit, -entityManager.GetComponentData<Health>(unit).Max);
            }
        }

        /// <summary>Kills <paramref name="units"/> through the damage system, so deaths play out as in a fight.</summary>
        public static void Destroy(EntityManager entityManager, NativeArray<Entity> units)
        {
            foreach (var unit in units)
            {
                Hit(entityManager, unit, LethalDamage);
            }
        }

        private static void Hit(EntityManager entityManager, Entity unit, float amount)
        {
            using var queue = entityManager.CreateEntityQuery(typeof(DamageQueue));
            if (!queue.TryGetSingletonEntity<DamageQueue>(out var events))
            {
                return;
            }

            var position = entityManager.GetComponentData<LocalTransform>(unit).Position;
            entityManager.GetBuffer<DamageEvent>(events)
                .Add(DamageEvent.Direct(unit, position, Entity.Null, Owner.Neutral, amount));
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

            var center = map.Center.xz;
            return new float3(center.x, terrain.Height(center), center.y);
        }
    }
}
