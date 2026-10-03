using HyperRTS.Simulation.Interaction;
using HyperRTS.Simulation.Match;
using HyperRTS.Simulation.Resources;
using HyperRTS.Simulation.Vision;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;

namespace HyperRTS.Input.Commands
{
    /// <summary>Resolves a screen point to the commandable entity and the ground point under it.</summary>
    public static class WorldPointer
    {
        /// <summary>
        /// Raycasts physics when available. Only ownable entities and resource nodes count as targets; anything
        /// else hit (terrain) supplies the ground point, otherwise the ray meets the y = 0 plane.
        /// </summary>
        public static bool TryPick(Camera camera, float2 screen, bool hasPhysics, in PhysicsWorldSingleton physics,
            EntityManager entityManager, out Entity target, out float3 ground)
        {
            target = Entity.Null;
            var ray = camera.ScreenPointToRay(new Vector3(screen.x, screen.y, 0f));
            var origin = (float3)ray.origin;
            var direction = math.normalizesafe((float3)ray.direction);

            var rayInput = new RaycastInput
            {
                Start = origin,
                End = origin + direction * camera.farClipPlane,
                Filter = CollisionFilter.Default,
            };

            if (hasPhysics && physics.CastRay(rayInput, out var hit))
            {
                if (!IsTarget(entityManager, hit.Entity))
                {
                    ground = hit.Position;
                    return true;
                }

                target = hit.Entity;
                if (!CommandMath.TryGroundPoint(origin, direction, 0f, out ground))
                {
                    ground = hit.Position;
                }

                return true;
            }

            return CommandMath.TryGroundPoint(origin, direction, 0f, out ground);
        }

        private static bool IsTarget(EntityManager entityManager, Entity entity)
        {
            if (entityManager.HasComponent<ResourceNode>(entity))
            {
                return true;
            }

            return entityManager.HasComponent<Faction>(entity) && IsVisibleToLocalTeam(entityManager, entity);
        }

        // Enemies hidden by fog must not be targetable, or a click would reveal and attack them.
        private static bool IsVisibleToLocalTeam(EntityManager entityManager, Entity entity)
        {
            using var fogQuery = entityManager.CreateEntityQuery(typeof(FogOfWar));
            using var localQuery = entityManager.CreateEntityQuery(typeof(LocalPlayer), typeof(Player));
            if (!fogQuery.TryGetSingleton<FogOfWar>(out var fog) || !fog.IsCreated ||
                !localQuery.TryGetSingleton<Player>(out var local))
            {
                return true;
            }

            var faction = entityManager.GetComponentData<Faction>(entity).Value;
            return faction == local.Faction || faction == Faction.Neutral ||
                   fog.IsVisible(entityManager.GetComponentData<LocalTransform>(entity).Position, local.Team);
        }
    }
}
