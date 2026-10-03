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
    /// <summary>Resolves a screen point to the commandable entity and the ground point under it, for one system.</summary>
    public sealed class WorldPointer
    {
        private readonly EntityQuery _physics;
        private readonly EntityQuery _fog;
        private readonly EntityQuery _relations;
        private readonly EntityQuery _localPlayer;
        private Camera _camera;

        public WorldPointer(ref SystemState state)
        {
            _physics = state.GetEntityQuery(ComponentType.ReadOnly<PhysicsWorldSingleton>());
            _fog = state.GetEntityQuery(ComponentType.ReadOnly<FogOfWar>());
            _relations = state.GetEntityQuery(ComponentType.ReadOnly<FactionRelations>());
            _localPlayer = state.GetEntityQuery(ComponentType.ReadOnly<LocalPlayer>(), ComponentType.ReadOnly<Player>());
        }

        /// <summary>
        /// Raycasts physics when available. Only ownable entities and resource nodes count as targets; anything
        /// else hit (terrain) supplies the ground point, otherwise the ray meets the y = 0 plane.
        /// </summary>
        public bool TryPick(ref SystemState state, float2 screen, out Entity target, out float3 ground)
        {
            target = Entity.Null;
            ground = default;
            if (_camera == null)
            {
                _camera = Camera.main;
                if (_camera == null)
                {
                    return false;
                }
            }

            var ray = _camera.ScreenPointToRay(new Vector3(screen.x, screen.y, 0f));
            var origin = (float3)ray.origin;
            var direction = math.normalizesafe((float3)ray.direction);
            if (!TryCast(ref state, origin, direction * _camera.farClipPlane, out var hit))
            {
                return CommandMath.TryGroundPoint(origin, direction, 0f, out ground);
            }

            if (!IsTarget(state.EntityManager, hit.Entity))
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

        private bool TryCast(ref SystemState state, float3 origin, float3 delta, out Unity.Physics.RaycastHit hit)
        {
            hit = default;
            if (!_physics.TryGetSingleton(out PhysicsWorldSingleton physics))
            {
                return false;
            }

            state.CompleteDependency();
            var input = new RaycastInput { Start = origin, End = origin + delta, Filter = CollisionFilter.Default };
            return physics.CastRay(input, out hit);
        }

        private bool IsTarget(EntityManager entityManager, Entity entity)
        {
            if (entityManager.HasComponent<ResourceNode>(entity))
            {
                return true;
            }

            return entityManager.HasComponent<Faction>(entity) && !IsHiddenByFog(entityManager, entity);
        }

        // Enemies hidden by fog must not be targetable, or a click would reveal and attack them.
        private bool IsHiddenByFog(EntityManager entityManager, Entity entity)
        {
            if (!_fog.TryGetSingleton(out FogOfWar fog) || !fog.IsCreated ||
                !_relations.TryGetSingleton(out FactionRelations relations) ||
                !_localPlayer.TryGetSingleton(out Player local))
            {
                return false;
            }

            var faction = entityManager.GetComponentData<Faction>(entity).Value;
            var position = entityManager.GetComponentData<LocalTransform>(entity).Position;
            return fog.IsHiddenFrom(in relations, local.Faction, faction, position);
        }
    }
}
