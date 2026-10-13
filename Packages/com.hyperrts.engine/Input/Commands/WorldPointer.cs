using HyperRTS.Input.Cameras;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Navigation;
using HyperRTS.Simulation.Resources;
using HyperRTS.Simulation.Selection;
using HyperRTS.Simulation.Vision;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace HyperRTS.Input.Commands
{
    /// <summary>Resolves a screen point to the commandable entity and the ground point under it, for one system.</summary>
    public sealed class WorldPointer
    {
        private readonly EntityQuery _terrain;
        private PickRanking _ranking;
        private Camera _camera;

        public WorldPointer(ref SystemState state)
        {
            _terrain = state.GetEntityQuery(ComponentType.ReadOnly<TerrainHeight>());
            _ranking = new PickRanking(ref state);
        }

        /// <summary>
        /// Picks through <see cref="PickRanking"/> when physics is available. Only ownable entities and resource nodes
        /// count as targets; anything else picked (terrain) supplies the ground point, otherwise the ray meets the
        /// baked terrain or the y = 0 plane.
        /// </summary>
        public bool TryPick(ref SystemState state, float2 screen, out Entity target, out float3 ground)
        {
            target = Entity.Null;
            ground = default;
            if (!TryRay(screen, out var origin, out var direction))
            {
                return false;
            }

            var ray = PickRanking.Ray(origin, direction, _camera.farClipPlane);
            var screenSize = new float2(Screen.width, Screen.height);
            var viewProjection = CameraMatrices.ViewProjection(_camera);
            if (!_ranking.TryPick(ref state, ray, screen, viewProjection, screenSize, out var picked, out var point))
            {
                return RaycastTerrain(origin, direction, out ground);
            }

            if (!IsTarget(state.EntityManager, picked))
            {
                ground = point;
                return true;
            }

            target = picked;
            if (!RaycastTerrain(origin, direction, out ground))
            {
                ground = point;
            }

            return true;
        }

        /// <summary>The terrain point under the screen point, ignoring entities (for placement).</summary>
        public bool TryGround(float2 screen, out float3 ground)
        {
            ground = default;
            if (!TryRay(screen, out var origin, out var direction))
            {
                return false;
            }

            return RaycastTerrain(origin, direction, out ground);
        }

        private bool TryRay(float2 screen, out float3 origin, out float3 direction)
        {
            origin = default;
            direction = default;
            if (_camera == null)
            {
                _camera = Camera.main;
                if (_camera == null)
                {
                    return false;
                }
            }

            var ray = _camera.ScreenPointToRay(new Vector3(screen.x, screen.y, 0f));
            origin = ray.origin;
            direction = math.normalizesafe((float3)ray.direction);
            return true;
        }

        private bool RaycastTerrain(float3 origin, float3 direction, out float3 ground)
        {
            // Without a baked terrain the default heightfield is the y = 0 plane.
            _terrain.TryGetSingleton(out TerrainHeight terrain);
            return terrain.Raycast(origin, direction, _camera.farClipPlane, out ground);
        }

        private bool IsTarget(EntityManager entityManager, Entity entity)
        {
            if (entityManager.HasComponent<ResourceNode>(entity))
            {
                return true;
            }

            // Enemies hidden by fog must not be targetable, or a click would reveal and attack them.
            return entityManager.HasComponent<Faction>(entity) && !entityManager.HasComponent<FogHidden>(entity);
        }
    }
}
