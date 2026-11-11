using Unity.Mathematics;

namespace HyperRTS.Simulation.Navigation.Drive
{
    internal partial struct DrivePlanner
    {
        // The last stretch drives as drawn, so a unit ends on its goal facing whichever way it arrived.
        private void PlanEnd()
        {
            if (!WaitsForBudget)
            {
                _writer.AddDrawn(_line, float.PositiveInfinity);
            }
        }

        private readonly bool CanDrive(float from, float to) => TryCurve(from, to, out _, out _);

        private readonly bool TryCurve(float from, float to, out DriveCurve curve, out float length)
        {
            var start = PoseAt(from);
            if (!_hull.TryFit(start, GoalAt(to), out curve, out length))
            {
                return false;
            }

            return _hull.IsClear(curve, length, math.max(_hull.Depth(start, float.PositiveInfinity), SweepSlack));
        }

        private readonly AxlePose PoseAt(float distance) =>
            distance <= 0f ? _start : Chassis.Pose(_line.PointAt(distance), _line.HeadingAt(distance));

        // A pose along the path; at its end the point.
        private readonly DriveGoal GoalAt(float distance) =>
            distance < _line.Total - 1e-3f
                ? DriveGoal.ToPose(_line.PointAt(distance), _line.HeadingAt(distance))
                : DriveGoal.ToPoint(_line.End);
    }
}
