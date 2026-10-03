using HyperRTS.Simulation.Interaction;
using HyperRTS.Simulation.Match;
using HyperRTS.Simulation.Resources;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
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

        private static bool IsTarget(EntityManager entityManager, Entity entity) =>
            entityManager.HasComponent<Faction>(entity) || entityManager.HasComponent<ResourceNode>(entity);
    }
}
