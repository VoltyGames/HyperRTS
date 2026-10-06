using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Navigation
{
    /// <summary>Walk-up helpers for order behaviours that act on a target entity (gather, build, deposit).</summary>
    public static class ReachMath
    {
        /// <summary>Pathing stops at the nearest walkable cell, which may sit up to a cell away from the footprint.</summary>
        public const float Slack = 1f;

        /// <summary>Targets without a <see cref="NavObstacle"/> get the same default extent as <see cref="EntityRadius"/>.</summary>
        public static float2 HalfExtents(in ComponentLookup<NavObstacle> obstacles, Entity target) =>
            obstacles.TryGetComponent(target, out var obstacle) ? obstacle.Size * 0.5f : new float2(EntityRadius.Default);

        /// <summary>True when the unit's edge is within <see cref="Slack"/> of the target's XZ footprint box.</summary>
        public static bool InReach(float3 unit, float unitRadius, float3 target, float2 halfExtents)
        {
            var outside = math.max(math.abs(unit.xz - target.xz) - halfExtents, 0f);
            return math.length(outside) <= unitRadius + Slack;
        }

        /// <summary>Walks toward the target until in reach, then stops; true once there.</summary>
        public static bool Approach(ref MoveDestination destination, EnabledRefRW<MoveDestination> moving, float3 unit,
            float unitRadius, float3 target, float2 halfExtents)
        {
            if (!InReach(unit, unitRadius, target, halfExtents))
            {
                MoveTo(ref destination, moving, target);
                return false;
            }

            moving.ValueRW = false;
            return true;
        }

        /// <summary>
        /// Points locomotion at the goal, rewriting it only when it moved more than <paramref name="tolerance"/>, so
        /// pathing isn't restarted every frame.
        /// </summary>
        public static void MoveTo(ref MoveDestination destination, EnabledRefRW<MoveDestination> moving, float3 goal,
            float tolerance = 0.1f)
        {
            if (moving.ValueRO && math.distancesq(destination.Value.xz, goal.xz) < tolerance * tolerance)
            {
                return;
            }

            destination.Value = goal;
            moving.ValueRW = true;
        }
    }
}
