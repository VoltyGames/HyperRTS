using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Navigation;
using HyperRTS.Simulation.Resources;
using HyperRTS.Simulation.Selection;
using HyperRTS.Simulation.Vision;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using UnityEngine;

namespace HyperRTS.Input.Commands
{
    /// <summary>Resolves a screen point to the commandable entity and the ground point under it, for one system.</summary>
    public sealed class WorldPointer
    {
        private readonly EntityQuery _physics;
        private readonly EntityQuery _terrain;
        private readonly EntityQuery _units;
        private PickRanking _ranking;
        private Camera _camera;

        public WorldPointer(ref SystemState state)
        {
            _physics = state.GetEntityQuery(ComponentType.ReadOnly<PhysicsWorldSingleton>());
            _terrain = state.GetEntityQuery(ComponentType.ReadOnly<TerrainHeight>());
            _units = state.GetEntityQuery(ComponentType.ReadOnly<Selectable>(), ComponentType.ReadOnly<UnitTag>());
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
            if (!TryRank(ref state, screen, origin, direction, out var picked, out var point))
            {
                return TryGround(origin, direction, out ground);
            }

            if (!IsTarget(state.EntityManager, picked))
            {
                ground = point;
                return true;
            }

            target = picked;
            if (!TryGround(origin, direction, out ground))
            {
                ground = point;
            }

            return true;
        }

        private bool TryGround(float3 origin, float3 direction, out float3 ground)
        {
            // Without a baked terrain the default heightfield is the y = 0 plane.
            _terrain.TryGetSingleton(out TerrainHeight terrain);
            return terrain.Raycast(origin, direction, _camera.farClipPlane, out ground);
        }

        private bool TryRank(ref SystemState state, float2 screen, float3 origin, float3 direction, out Entity picked,
            out float3 point)
        {
            picked = Entity.Null;
            point = default;
            if (!_physics.TryGetSingleton(out PhysicsWorldSingleton physics))
            {
                return false;
            }

            state.CompleteDependency();
            _ranking.Update(ref state);
            var input = new RaycastInput
            {
                Start = origin,
                End = origin + direction * _camera.farClipPlane,
                Filter = CollisionFilter.Default,
            };
            var hits = new NativeList<Unity.Physics.RaycastHit>(Allocator.Temp);
            physics.CastRay(input, ref hits);
            using var units = _units.ToEntityArray(Allocator.Temp);
            var screenSize = new float2(Screen.width, Screen.height);
            return _ranking.TryPick(hits.AsArray(), units, screen, ViewProjection(), screenSize, out picked, out point);
        }

        private float4x4 ViewProjection()
        {
            var vp = _camera.projectionMatrix * _camera.worldToCameraMatrix;
            return new float4x4(vp.GetColumn(0), vp.GetColumn(1), vp.GetColumn(2), vp.GetColumn(3));
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
