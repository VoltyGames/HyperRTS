using HyperRTS.Simulation.Navigation.Paths;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Navigation.Drive
{
    internal partial struct DrivePlanner
    {
        private const float RunStep = 1.5f;

        private readonly bool HasPoseEnd => math.any(_endFacing != 0f);

        // Out of a dock bay the path starts straight ahead, so the hull never turns inside the bay.
        private void AddExitRun(in PathRequest request)
        {
            if (request.ExitRun <= 0f)
            {
                return;
            }

            var from = request.Start.xz;
            for (var distance = RunStep; distance < request.ExitRun; distance += RunStep)
            {
                _writer.AddTight(from + request.Facing * distance);
            }

            _writer.AddTight(request.RouteStart.xz);
            _start = Chassis.Pose(request.RouteStart.xz, request.Facing);
        }

        // A route cut short of its goal ends as drawn. The run-in shrinks to what lies ahead of the start, so a unit
        // already on its line drives straight on.
        private void SetEnd(in PathRequest request)
        {
            _endFacing = float2.zero;
            _runIn = 0f;
            var end = _line.End;
            if (_line.Corners.Length < 2 || math.distancesq(end, request.Goal.xz) > 0.01f)
            {
                return;
            }

            _endFacing = math.normalizesafe(request.GoalFacing);
            var ahead = math.dot(end - Chassis.Center(_start), _endFacing);
            _runIn = math.clamp(ahead, 0f, request.RunIn);
        }

        // A pose end replaces the path's last stretch with the forward curve, or a search, to where its run-in
        // starts, then the run-in straight. A failed search leaves the end as drawn.
        private void PlanEnd()
        {
            if (WaitsForBudget)
            {
                return;
            }

            if (HasPoseEnd && _writer.Written < _line.Total - 1e-3f)
            {
                var from = math.max(_writer.Written, _line.Total - _runIn - 2f * Chassis.TurnRadius - HullLength);
                if (!TryAddCurve(from))
                {
                    Search(from, _line.Total);
                }
            }

            if (!HasPoseEnd || _writer.Written < _line.Total - 1e-3f)
            {
                _writer.AddDrawn(_line, float.PositiveInfinity);
                return;
            }

            var stop = _line.End;
            for (var distance = _runIn - RunStep; distance > 0f; distance -= RunStep)
            {
                _writer.AddTight(stop - _endFacing * distance);
            }

            _writer.AddTight(stop);
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

        private bool TryAddCurve(float from)
        {
            if (!TryCurve(from, _line.Total, out var curve, out var length))
            {
                return false;
            }

            _writer.BeginStretch(_line, from);
            for (var distance = RunStep; distance < length; distance += RunStep)
            {
                _writer.AddTight(Chassis.Center(curve.Sample(distance)));
            }

            _writer.AddTight(Chassis.Center(curve.Sample(length)));
            _writer.Written = _line.Total;
            return true;
        }

        private readonly AxlePose PoseAt(float distance) =>
            distance <= 0f ? _start : Chassis.Pose(_line.PointAt(distance), _line.HeadingAt(distance));

        // A pose along the path; at its end the point, or for a pose end where its run-in starts.
        private readonly DriveGoal GoalAt(float distance)
        {
            if (distance < _line.Total - 1e-3f)
            {
                return DriveGoal.ToPose(_line.PointAt(distance), _line.HeadingAt(distance));
            }

            var end = _line.End;
            return HasPoseEnd ? DriveGoal.ToPose(end - _endFacing * _runIn, _endFacing) : DriveGoal.ToPoint(end);
        }
    }
}
