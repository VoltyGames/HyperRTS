using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Navigation
{
    /// <summary>Walk-up helpers for order behaviours that act on a target entity (gather, build, deposit) or its dock bays.</summary>
    public static class ReachMath
    {
        /// <summary>How far a unit's edge may be from the nearest it can get to a footprint.</summary>
        public const float Slack = 1f;

        // A vehicle parked this near its place in line stays put rather than creeping onto it.
        private const float LineSlack = 1.5f;

        /// <summary>
        /// Gap from a unit's core segment to a footprint within which it is at it. Closed cells reach up to a cell past
        /// the footprint and the unit keeps its radius off them, so its edge is allowed a cell more than the slack.
        /// </summary>
        public static float Reach(float unitRadius, float navCellSize) => unitRadius + Slack + navCellSize;

        /// <summary>Whether the unit's capsule round <paramref name="unit"/> is at the footprint.</summary>
        public static bool InReach(in NavBox footprint, float2 unit, float2 axis, float unitRadius,
            float navCellSize) =>
            footprint.Gap(unit - axis, unit + axis) <= Reach(unitRadius, navCellSize);

        /// <summary>
        /// Drives a vehicle along the bay's run-in until it stands at the stop. A drive that gave up short of the stop
        /// is stuck; one that ended off the stop's pose plans the rest from where it is.
        /// </summary>
        public static WalkUp ApproachBay(MoveControl move, in BayPose bay, float2 unit, float2 facing)
        {
            if (bay.IsAtStop(unit, facing))
            {
                move.Stop();
                return WalkUp.Reached;
            }

            var isGoalStop = math.distancesq(move.Destination.ValueRO.Value.xz, bay.Stop.xz) < 0.01f;
            if (isGoalStop && move.GaveUp)
            {
                return WalkUp.Stuck;
            }

            move.ToPose(bay.Stop, bay.Facing, bay.RunIn);
            move.Arrived.ValueRW = false;
            return WalkUp.Walking;
        }

        /// <summary>Parks a vehicle at its place in the bay's line, facing the bay, and keeps it there.</summary>
        public static void WaitInLine(MoveControl move, in BayPose bay, int place, float hullLength, float2 unit)
        {
            var spot = bay.WaitSpot(place, hullLength);
            move.Arrived.ValueRW = false;
            if (!move.Moving.ValueRO && math.distance(unit, spot.xz) <= LineSlack)
            {
                return;
            }

            move.ToPose(spot, bay.Facing, 0f);
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

            destination = new MoveDestination { Value = goal, Chase = true };
            moving.ValueRW = true;
        }
    }
}
