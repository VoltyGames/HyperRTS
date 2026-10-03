using HyperRTS.Simulation.Navigation;
using HyperRTS.Simulation.Units;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Buildings
{
    /// <summary>Walk-up helpers for order behaviours that act on a target entity (gather, build, deposit).</summary>
    public static class ReachMath
    {
        /// <summary>Footprint assumed for targets without a <see cref="NavObstacle"/>.</summary>
        public const float DefaultFootprint = 1.5f;

        /// <summary>Pathing stops at the nearest walkable cell, which may sit up to a cell away from the footprint.</summary>
        public const float Slack = 1f;

        public static float2 HalfExtents(in ComponentLookup<NavObstacle> obstacles, Entity target) =>
            (obstacles.TryGetComponent(target, out var obstacle) ? obstacle.Size : new float2(DefaultFootprint)) * 0.5f;

        /// <summary>True when the unit's edge is within <see cref="Slack"/> of the target's XZ footprint box.</summary>
        public static bool InReach(float3 unit, float unitRadius, float3 target, float2 halfExtents)
        {
            var outside = math.max(math.abs(unit.xz - target.xz) - halfExtents, 0f);
            return math.length(outside) <= unitRadius + Slack;
        }

        /// <summary>Points locomotion at the goal, rewriting it only on change so pathing isn't restarted every frame.</summary>
        public static void MoveTo(ref MoveDestination destination, EnabledRefRW<MoveDestination> moving, float3 goal)
        {
            if (moving.ValueRO && math.distancesq(destination.Value.xz, goal.xz) < 0.01f)
            {
                return;
            }

            destination.Value = goal;
            moving.ValueRW = true;
        }
    }
}
