using Unity.Mathematics;

namespace HyperRTS.Simulation.Navigation
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
                AddTight(from + request.Facing * distance);
            }

            AddTight(request.RouteStart.xz);
            _start = Chassis.Pose(request.RouteStart.xz, request.Facing);
        }

        // A route cut short of its goal ends as drawn. The run-in shrinks to what lies ahead of the start, so a unit
        // already on its line drives straight on.
        private void SetEnd(in PathRequest request)
        {
            _endFacing = float2.zero;
            _runIn = 0f;
            var end = _corners[_corners.Length - 1];
            if (_corners.Length < 2 || math.distancesq(end, request.Goal.xz) > 0.01f)
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

            if (HasPoseEnd && _written < Total - 1e-3f)
            {
                var from = math.max(_written, Total - _runIn - 2f * Chassis.TurnRadius - HullLength);
                if (!TryAddCurve(from))
                {
                    Search(from, Total);
                }
            }

            if (!HasPoseEnd || _written < Total - 1e-3f)
            {
                AddDrawn(float.PositiveInfinity);
                return;
            }

            var stop = _corners[_corners.Length - 1];
            for (var distance = _runIn - RunStep; distance > 0f; distance -= RunStep)
            {
                AddTight(stop - _endFacing * distance);
            }

            AddTight(stop);
        }

        private readonly bool CanDrive(float from, float to) => TryCurve(from, to, out _, out _);

        private readonly bool TryCurve(float from, float to, out DriveCurve curve, out float length)
        {
            var start = PoseAt(from);
            if (!_search.TryFit(start, GoalAt(to), out curve, out length))
            {
                return false;
            }

            return _search.IsClear(curve, length, math.max(_search.Depth(start, float.PositiveInfinity), SweepSlack));
        }

        private bool TryAddCurve(float from)
        {
            if (!TryCurve(from, Total, out var curve, out var length))
            {
                return false;
            }

            BeginStretch(from);
            for (var distance = RunStep; distance < length; distance += RunStep)
            {
                AddTight(Chassis.Center(curve.Sample(distance)));
            }

            AddTight(Chassis.Center(curve.Sample(length)));
            _written = Total;
            return true;
        }

        // A stretch that doesn't follow on from the last starts at its pose on the path.
        private void BeginStretch(float from)
        {
            var joins = from <= _written;
            AddDrawn(from);
            if (!joins)
            {
                AddTight(PointAt(from));
            }
        }

        private void AddTight(float2 point) =>
            _output.Add(new PathWaypoint { Position = new float3(point.x, _height, point.y), Tight = true });
    }
}
