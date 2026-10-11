using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Navigation
{
    /// <summary>Walk-up helpers for order behaviours that act on a target entity (gather, build, deposit).</summary>
    public static class ReachMath
    {
        /// <summary>How far a unit's edge may be from the footprint and still be at it, whatever the nav grid.</summary>
        public const float Slack = 1f;

        /// <summary>
        /// Pathing parks a unit on the centre of the nearest open cell. A footprint blocks every cell it overlaps, so
        /// that centre can lie up to a cell and a half from its edge.
        /// </summary>
        public const float CellReach = 1.5f;

        /// <summary>Targets without a <see cref="NavObstacle"/> get the same default extent as <see cref="EntityRadius"/>.</summary>
        public static float2 HalfExtents(in ComponentLookup<NavObstacle> obstacles, Entity target) =>
            obstacles.TryGetComponent(target, out var obstacle) ? obstacle.Size * 0.5f : new float2(EntityRadius.Default);

        /// <summary>
        /// Distance from the target's footprint within which the unit is at it: its own edge within <see cref="Slack"/>,
        /// or on the nearest open cell of a grid of <paramref name="navCellSize"/> (0 without a grid).
        /// </summary>
        public static float Reach(float unitRadius, float navCellSize) =>
            math.max(unitRadius + Slack, navCellSize * CellReach);

        /// <summary>True when the unit is within <see cref="Reach"/> of the target's XZ footprint box.</summary>
        public static bool InReach(float3 unit, float unitRadius, float3 target, float2 halfExtents, float navCellSize)
        {
            var outside = math.max(math.abs(unit.xz - target.xz) - halfExtents, 0f);
            return math.length(outside) <= Reach(unitRadius, navCellSize);
        }

        /// <summary>Walks toward the target until in reach, then stops; true once there.</summary>
        public static bool Approach(ref MoveDestination destination, EnabledRefRW<MoveDestination> moving, float3 unit,
            float unitRadius, float3 target, float2 halfExtents, float navCellSize)
        {
            if (!InReach(unit, unitRadius, target, halfExtents, navCellSize))
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
