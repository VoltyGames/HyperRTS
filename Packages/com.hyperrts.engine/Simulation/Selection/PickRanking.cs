using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Navigation;
using HyperRTS.Simulation.Vision;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Selection
{
    /// <summary>
    /// Ranks what a pointer picks so units are never shadowed by buildings: a visible unit on the ray, else the
    /// visible unit nearest the pointer on screen, else the closest hit.
    /// </summary>
    public struct PickRanking
    {
        /// <summary>Small units still catch a click this many pixels from their centre.</summary>
        public const float MinScreenRadius = 10f;

        public ComponentLookup<Selectable> Selectables;
        public ComponentLookup<UnitTag> Units;
        public ComponentLookup<FogHidden> Hidden;
        public ComponentLookup<Inside> Inside;
        public ComponentLookup<LocalTransform> Transforms;
        public ComponentLookup<NavAgent> Agents;
        public ComponentLookup<NavObstacle> Obstacles;

        public PickRanking(ref SystemState state)
        {
            Selectables = state.GetComponentLookup<Selectable>(true);
            Units = state.GetComponentLookup<UnitTag>(true);
            Hidden = state.GetComponentLookup<FogHidden>(true);
            Inside = state.GetComponentLookup<Inside>(true);
            Transforms = state.GetComponentLookup<LocalTransform>(true);
            Agents = state.GetComponentLookup<NavAgent>(true);
            Obstacles = state.GetComponentLookup<NavObstacle>(true);
        }

        public void Update(ref SystemState state)
        {
            Selectables.Update(ref state);
            Units.Update(ref state);
            Hidden.Update(ref state);
            Inside.Update(ref state);
            Transforms.Update(ref state);
            Agents.Update(ref state);
            Obstacles.Update(ref state);
        }

        /// <summary>
        /// Picks among <paramref name="hits"/> (any order) and the on-screen <paramref name="units"/>; the point is
        /// the hit position, or the unit's position when it was picked on screen.
        /// </summary>
        public bool TryPick(NativeArray<RaycastHit> hits, NativeArray<Entity> units, float2 pointer,
            float4x4 viewProjection, float2 screenSize, out Entity entity, out float3 point)
        {
            if (TryClosestHit(hits, true, out var hit))
            {
                entity = hit.Entity;
                point = hit.Position;
                return true;
            }

            if (TryNearestOnScreen(units, pointer, viewProjection, screenSize, out entity))
            {
                point = Transforms[entity].Position;
                return true;
            }

            var any = TryClosestHit(hits, false, out hit);
            entity = hit.Entity;
            point = hit.Position;
            return any;
        }

        /// <summary>A selectable unit not hidden by fog and not inside a container.</summary>
        public bool IsVisibleUnit(Entity entity)
        {
            if (!Units.HasComponent(entity) || !Selectables.HasComponent(entity))
            {
                return false;
            }

            return !Hidden.HasComponent(entity) && !Inside.HasEnabled(entity);
        }

        private bool TryClosestHit(NativeArray<RaycastHit> hits, bool unitsOnly, out RaycastHit closest)
        {
            closest = default;
            var found = false;
            foreach (var hit in hits)
            {
                if (unitsOnly && !IsVisibleUnit(hit.Entity))
                {
                    continue;
                }

                if (!found || hit.Fraction < closest.Fraction)
                {
                    closest = hit;
                    found = true;
                }
            }

            return found;
        }

        private bool TryNearestOnScreen(NativeArray<Entity> units, float2 pointer, float4x4 viewProjection,
            float2 screenSize, out Entity nearest)
        {
            nearest = Entity.Null;
            var nearestDistance = float.MaxValue;
            foreach (var unit in units)
            {
                if (!IsVisibleUnit(unit))
                {
                    continue;
                }

                var position = Transforms[unit].Position;
                if (!SelectionMath.WorldToScreenPoint(viewProjection, position, screenSize, out var screen))
                {
                    continue;
                }

                var distance = math.distance(screen, pointer);
                var radius = EntityRadius.Get(unit, Agents, Obstacles);
                var reach = math.max(MinScreenRadius, ScreenRadius(viewProjection, screenSize, position, screen, radius));
                if (distance <= reach && distance < nearestDistance)
                {
                    nearest = unit;
                    nearestDistance = distance;
                }
            }

            return nearest != Entity.Null;
        }

        /// <summary>The footprint's radius in pixels, measured along both ground axes since the camera may yaw.</summary>
        private static float ScreenRadius(float4x4 viewProjection, float2 screenSize, float3 position, float2 center,
            float radius)
        {
            var reach = 0f;
            if (SelectionMath.WorldToScreenPoint(viewProjection, position + new float3(radius, 0f, 0f), screenSize, out var x))
            {
                reach = math.distance(x, center);
            }

            if (SelectionMath.WorldToScreenPoint(viewProjection, position + new float3(0f, 0f, radius), screenSize, out var z))
            {
                reach = math.max(reach, math.distance(z, center));
            }

            return reach;
        }
    }
}
