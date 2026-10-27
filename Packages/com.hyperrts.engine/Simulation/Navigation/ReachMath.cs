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
        /// Walks toward the footprint's nearest edge until in reach, then stops. A walk that locomotion ended by
        /// itself (its <see cref="Arrival"/>) short of a goal still by the footprint is stuck.
        /// </summary>
        public static WalkUp Approach(ref MoveDestination destination, EnabledRefRW<MoveDestination> moving,
            EnabledRefRW<Arrival> arrived, in NavBox footprint, float3 unit, float2 axis, float unitRadius,
            float navCellSize)
        {
            if (InReach(footprint, unit.xz, axis, unitRadius, navCellSize))
            {
                moving.ValueRW = false;
                arrived.ValueRW = false;
                return WalkUp.Reached;
            }

            // A goal already by the footprint stays put, so the path isn't restarted as the nearest edge slides.
            var goal = destination.Value.xz;
            var isGoalByFootprint = footprint.Gap(goal, goal) <= Reach(unitRadius, navCellSize);
            if (isGoalByFootprint && arrived.ValueRO && !moving.ValueRO)
            {
                return WalkUp.Stuck;
            }

            if (isGoalByFootprint && moving.ValueRO)
            {
                return WalkUp.Walking;
            }

            var edge = footprint.EdgePoint(unit.xz, unitRadius + Slack * 0.5f);
            MoveTo(ref destination, moving, new float3(edge.x, unit.y, edge.y));
            arrived.ValueRW = false;
            return WalkUp.Walking;
        }

        /// <summary>
        /// Drives a vehicle along the bay's run-in until it stands at the stop. A drive that gave up short of the stop
        /// is stuck; one that ended off the stop's pose plans the rest from where it is.
        /// </summary>
        public static WalkUp ApproachBay(ref MoveDestination destination, EnabledRefRW<MoveDestination> moving,
            EnabledRefRW<Arrival> arrived, ArrivalReason reason, in BayPose bay, float2 unit, float2 facing)
        {
            if (bay.IsAtStop(unit, facing))
            {
                moving.ValueRW = false;
                arrived.ValueRW = false;
                return WalkUp.Reached;
            }

            var isGoalStop = math.distancesq(destination.Value.xz, bay.Stop.xz) < 0.01f;
            var gaveUp = arrived.ValueRO && !moving.ValueRO && reason == ArrivalReason.Stalled;
            if (isGoalStop && gaveUp)
            {
                return WalkUp.Stuck;
            }

            MoveToPose(ref destination, moving, bay.Stop, bay.Facing, bay.RunIn);
            arrived.ValueRW = false;
            return WalkUp.Walking;
        }

        /// <summary>Parks a vehicle at its place in the bay's line, facing the bay, and keeps it there.</summary>
        public static void WaitInLine(ref MoveDestination destination, EnabledRefRW<MoveDestination> moving,
            EnabledRefRW<Arrival> arrived, in BayPose bay, int place, float hullLength, float2 unit)
        {
            var spot = bay.WaitSpot(place, hullLength);
            arrived.ValueRW = false;
            if (!moving.ValueRO && math.distance(unit, spot.xz) <= LineSlack)
            {
                return;
            }

            MoveToPose(ref destination, moving, spot, bay.Facing, 0f);
        }

        /// <summary>Points locomotion at a pose goal, rewriting it only when it changed, so pathing isn't restarted.</summary>
        public static void MoveToPose(ref MoveDestination destination, EnabledRefRW<MoveDestination> moving, float3 goal,
            float2 facing, float runIn)
        {
            var same = math.distancesq(destination.Value.xz, goal.xz) < 0.01f && math.all(destination.Facing == facing);
            if (moving.ValueRO && same)
            {
                return;
            }

            destination = new MoveDestination { Value = goal, Facing = facing, RunIn = runIn };
            moving.ValueRW = true;
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
