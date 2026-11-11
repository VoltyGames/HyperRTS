using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Navigation.Agents;
using HyperRTS.Simulation.Navigation.Grid;
using Unity.Collections;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Navigation.Drive
{
    /// <summary>The path a <see cref="DrivePlanner"/> checks and whether this hull drives its legs and turns as drawn.</summary>
    internal struct DriveCheck
    {
        // A drawn leg's hull may graze walls by this much where the field blends between cell centres.
        private const float SweepSlack = 0.05f;
        private static readonly float PivotSample = math.radians(10f);

        public NavAgent Agent;
        public DriveHull Hull;
        public PathLine Line;
        public AxlePose Start;

        public DriveCheck(Allocator allocator)
        {
            Agent = default;
            Hull = default;
            Line = new PathLine(allocator);
            Start = default;
        }

        public void Begin(in NavAgent agent, in NavAccess access)
        {
            Agent = agent;
            Hull = new DriveHull(access, agent);
        }

        public void Dispose() => Line.Dispose();

        private readonly Chassis Chassis => Agent.Chassis;

        public readonly bool CanDrive(float from, float to) => TryCurve(from, to, out _, out _);

        private readonly bool TryCurve(float from, float to, out DriveCurve curve, out float length)
        {
            var start = PoseAt(from);
            if (!Hull.TryFit(start, GoalAt(to), out curve, out length))
            {
                return false;
            }

            return Hull.IsClear(curve, length, math.max(Hull.Depth(start, float.PositiveInfinity), SweepSlack));
        }

        public readonly AxlePose PoseAt(float distance) =>
            distance <= 0f ? Start : Chassis.Pose(Line.PointAt(distance), Line.HeadingAt(distance));

        // A pose along the path; at its end the point.
        public readonly DriveGoal GoalAt(float distance) =>
            distance < Line.Total - 1e-3f
                ? DriveGoal.ToPose(Line.PointAt(distance), Line.HeadingAt(distance))
                : DriveGoal.ToPoint(Line.End);

        public readonly bool CanStart(float reach)
        {
            if (Chassis.IsWheeled)
            {
                return CanDrive(0f, reach);
            }

            return CanTurnInPlace(Start, Line.LegHeading(0)) || CanDrive(0f, reach);
        }

        public readonly bool CanPivot(int corner)
        {
            var pose = Chassis.Pose(Line.Corners[corner], Line.LegHeading(corner - 1));
            return CanTurnInPlace(pose, Line.LegHeading(corner));
        }

        // The whole sweep of the pivot, sliding off a wall an end swings into as locomotion does.
        private readonly bool CanTurnInPlace(in AxlePose pose, float2 toward)
        {
            var turn = Planar.SignedAngle(pose.Facing, toward);
            var steps = (int)math.ceil(math.abs(turn) / PivotSample);
            var center = Chassis.Center(pose);
            var axis = pose.Facing * Agent.HalfSegment;
            for (var step = 1; step <= steps; step++)
            {
                var hull = new NavCapsule(Hull.Access, axis);
                var next = Chassis.Rotate(pose, turn * step / steps).Facing * Agent.HalfSegment;
                var sweep = Agent.HalfSegment * math.abs(turn) / steps;
                if (!hull.CanTurn(center, next) && !hull.TryTurnOff(center, next, sweep, out center))
                {
                    return false;
                }

                axis = next;
            }

            return true;
        }
    }
}
