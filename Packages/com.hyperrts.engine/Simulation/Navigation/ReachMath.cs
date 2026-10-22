using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Navigation
{
    /// <summary>Walk-up helpers for order behaviours that act on a target entity (gather, build, deposit).</summary>
    public static class ReachMath
    {
        /// <summary>How far a unit's edge may be from the nearest it can get to a footprint.</summary>
        public const float Slack = 1f;

        /// <summary>
        /// Gap from a unit's core segment to a footprint within which it is at it. Closed cells reach up to a cell past
        /// the footprint and the unit keeps its radius off them, so its edge is allowed a cell more than the slack.
        /// </summary>
        public static float Reach(float unitRadius, float navCellSize) => unitRadius + Slack + navCellSize;

        /// <summary>Whether the unit's capsule round <paramref name="unit"/> is at the footprint.</summary>
        public static bool InReach(in NavBox footprint, float2 unit, float2 axis, float unitRadius,
            float navCellSize) =>
            footprint.Gap(unit - axis, unit + axis) <= Reach(unitRadius, navCellSize);

        /// <summary>Walks toward the footprint's nearest edge until in reach, then stops; true once there.</summary>
        public static bool Approach(ref MoveDestination destination, EnabledRefRW<MoveDestination> moving,
            in NavBox footprint, float3 unit, float2 axis, float unitRadius, float navCellSize)
        {
            if (InReach(footprint, unit.xz, axis, unitRadius, navCellSize))
            {
                moving.ValueRW = false;
                return true;
            }

            // A goal already by the footprint stays put, so the path isn't restarted as the nearest edge slides.
            var goal = destination.Value.xz;
            if (moving.ValueRO && footprint.Gap(goal, goal) <= Reach(unitRadius, navCellSize))
            {
                return false;
            }

            var edge = footprint.EdgePoint(unit.xz, unitRadius + Slack * 0.5f);
            MoveTo(ref destination, moving, new float3(edge.x, unit.y, edge.y));
            return false;
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

            destination = new MoveDestination { Value = goal };
            moving.ValueRW = true;
        }
    }
}
