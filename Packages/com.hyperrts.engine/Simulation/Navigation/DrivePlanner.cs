using HyperRTS.Simulation.Common;
using System;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Profiling;

namespace HyperRTS.Simulation.Navigation
{
    /// <summary>
    /// Checks a vehicle can drive a fresh path and plans the stretches it can't with a <see cref="DriveSearch"/> into
    /// <see cref="PathWaypoint.Tight"/> waypoints.
    /// </summary>
    internal partial struct DrivePlanner : IDisposable
    {
        // A drawn leg's hull may graze walls by this much where the field blends between cell centres.
        private const float SweepSlack = 0.05f;
        private const float StraightTurn = 0.01f;
        private static readonly float PivotSample = math.radians(10f);

        private static readonly ProfilerMarker PlanMarker = new("DrivePlanner.Plan");

        private readonly NavAgent _agent;
        private readonly NavAccess _access;
        private DriveSearch _search;
        private NativeList<float2> _corners;
        private NativeList<float> _distances;
        private NativeList<PathWaypoint> _tight;
        private NativeList<PathWaypoint> _output;
        private AxlePose _start;
        private float _height;

        // The facing a pose end stops with, zero for a point end, and the straight run into it.
        private float2 _endFacing;
        private float _runIn;

        // Path distance the output's waypoints already cover.
        private float _written;

        // Expansions this plan may spend, and whether running out defers it rather than leaving stretches as drawn.
        private int _budget;
        private bool _defers;
        private bool _planned;

        public int Searches;
        public int Expansions;
        public int Failures;

        /// <summary>The budget ran out before a search this plan needed, so it waits for a later frame.</summary>
        public bool WaitsForBudget;

        /// <summary>The budget ran out before a later stretch, left as drawn until the planned part is driven.</summary>
        public bool PartlyPlanned;

        public DrivePlanner(in NavAccess access, in NavAgent agent, Allocator allocator)
        {
            _agent = agent;
            _access = access;
            _search = new DriveSearch(access, agent, DriveSearch.MaxExpansions, allocator);
            _corners = new NativeList<float2>(16, allocator);
            _distances = new NativeList<float>(16, allocator);
            _tight = new NativeList<PathWaypoint>(64, allocator);
            _output = default;
            _start = default;
            _height = 0f;
            _endFacing = float2.zero;
            _runIn = 0f;
            _written = 0f;
            _budget = 0;
            _defers = false;
            _planned = false;
            Searches = 0;
            Expansions = 0;
            Failures = 0;
            WaitsForBudget = false;
            PartlyPlanned = false;
        }

        public void Dispose()
        {
            _search.Dispose();
            _corners.Dispose();
            _distances.Dispose();
            _tight.Dispose();
        }

        /// <summary>Whether its paths need checking: it has a turn radius or a hull longer than it is wide.</summary>
        public static bool Applies(in NavAgent agent) => agent.Chassis.TurnRadius > 0f || agent.HalfSegment > 0f;

        private readonly Chassis Chassis => _agent.Chassis;

        private readonly float HullLength => _agent.HalfLength * 2f;

        private readonly float Total => _distances[_distances.Length - 1];

        /// <summary>
        /// Writes the path's waypoints, with the stretches it had to plan made tight, spending at most
        /// <paramref name="budget"/> expansions. Past it, a plan that <paramref name="defers"/> stops and is
        /// <see cref="WaitsForBudget"/>; one that doesn't leaves the rest as drawn. False when deferred.
        /// </summary>
        public bool Plan(in PathRequest request, NativeList<float3> points, NativeList<PathWaypoint> waypoints,
            int budget, bool defers)
        {
            using var scope = PlanMarker.Auto();
            _output = waypoints;
            _height = request.Start.y;
            _start = Chassis.Pose(request.Start.xz, request.Facing);
            _written = 0f;
            _budget = budget;
            _defers = defers;
            _planned = false;
            WaitsForBudget = false;
            PartlyPlanned = false;
            AddExitRun(request);
            Trace(request.RouteStart.xz, points);
            SetEnd(request);
            if (_corners.Length < 2)
            {
                foreach (var point in points)
                {
                    _output.Add(new PathWaypoint { Position = new float3(point.x, _height, point.z) });
                }

                return true;
            }

            var cursor = PlanStart(request.Stuck);
            for (var corner = 1; corner < _corners.Length - 1 && !WaitsForBudget; corner++)
            {
                cursor = PlanCorner(corner, cursor);
            }

            PlanEnd();
            return !WaitsForBudget;
        }

        // Wheels drive the curve to a pose well along the path; tracks turn in place to the first leg.
        private float PlanStart(bool stuck)
        {
            var reach = math.min(Total, 2f * Chassis.TurnRadius + HullLength);
            if (!stuck && CanStart(reach))
            {
                return Chassis.IsWheeled ? reach : 0f;
            }

            return Search(0f, reach);
        }

        private readonly bool CanStart(float reach)
        {
            if (Chassis.IsWheeled)
            {
                return CanDrive(0f, reach);
            }

            return CanTurnInPlace(_start, LegHeading(0)) || CanDrive(0f, reach);
        }

        // Sweeps the curve from a fillet and a hull before the corner to as far after; tracks may pivot on it instead.
        private float PlanCorner(int corner, float cursor)
        {
            var at = _distances[corner];
            var turn = TurnAt(corner);
            var radius = _search.CurveRadius;
            var fillet = math.min(radius * math.tan(turn * 0.5f), radius * 2f);
            if (turn < StraightTurn || at + fillet <= cursor)
            {
                return cursor;
            }

            if (!Chassis.IsWheeled && CanPivot(corner))
            {
                return cursor;
            }

            var from = math.max(cursor, at - fillet - HullLength);
            var to = math.min(Total, at + fillet + HullLength);
            return CanDrive(from, to) ? to : Search(from, to);
        }

        private readonly bool CanPivot(int corner)
        {
            return CanTurnInPlace(Chassis.Pose(_corners[corner], LegHeading(corner - 1)), LegHeading(corner));
        }

        // The whole sweep of the pivot, sliding off a wall an end swings into as locomotion does.
        private readonly bool CanTurnInPlace(in AxlePose pose, float2 toward)
        {
            var turn = Planar.SignedAngle(pose.Facing, toward);
            var steps = (int)math.ceil(math.abs(turn) / PivotSample);
            var center = Chassis.Center(pose);
            var axis = pose.Facing * _agent.HalfSegment;
            for (var step = 1; step <= steps; step++)
            {
                var hull = new NavCapsule(_access, axis);
                var next = Chassis.Rotate(pose, turn * step / steps).Facing * _agent.HalfSegment;
                var sweep = _agent.HalfSegment * math.abs(turn) / steps;
                if (!hull.CanTurn(center, next) && !hull.TryTurnOff(center, next, sweep, out center))
                {
                    return false;
                }

                axis = next;
            }

            return true;
        }

        // Retries reaching twice as far on, where the hull may find room to turn, then stays as drawn and fails.
        private float Search(float from, float to)
        {
            var further = math.min(Total, to + (to - from) * 2f);
            var retries = further > to;
            var half = DriveSearch.MaxExpansions / 2;
            var found = TrySearch(from, to, retries ? half : DriveSearch.MaxExpansions, out var cut);
            var failed = !found && !cut;
            if (failed && retries)
            {
                to = further;
                found = TrySearch(from, to, half, out cut);
            }

            if (!found)
            {
                GiveUp(cut);
                return to;
            }

            _planned = true;
            BeginStretch(from);
            _output.AddRange(_tight.AsArray());
            _written = to;
            return to;
        }

        // Out of budget, a plan that may wait does, and the frame's first plans the rest once its planned part is
        // driven, unless it planned nothing yet, which would plan the same stretch every frame.
        private void GiveUp(bool cut)
        {
            WaitsForBudget = cut && _defers;
            PartlyPlanned |= cut && !_defers && _planned;
            Failures += !cut || (!_defers && !_planned) ? 1 : 0;
        }

        // Cut when what is left of the budget stopped it short of the usual cap.
        private bool TrySearch(float from, float to, int most, out bool cut)
        {
            var cap = math.min(most, _budget - Expansions);
            cut = cap <= 0;
            if (cut)
            {
                return false;
            }

            Searches++;
            _tight.Clear();
            _search.Cap = cap;
            var found = _search.Run(PoseAt(from), GoalAt(to), _height, _tight);
            Expansions += _search.Expansions;
            cut = !found && cap < most && _search.Expansions >= cap;
            return found;
        }

        // The drawn corners after what is written and before the given distance.
        private void AddDrawn(float before)
        {
            for (var corner = 1; corner < _corners.Length; corner++)
            {
                var at = _distances[corner];
                if (at > _written && at < before)
                {
                    var point = _corners[corner];
                    _output.Add(new PathWaypoint { Position = new float3(point.x, _height, point.y) });
                }
            }

            _written = math.max(_written, math.min(before, Total));
        }
    }
}
