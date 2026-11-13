using HyperRTS.Simulation.Navigation.Agents;
using HyperRTS.Simulation.Navigation.Grid;
using HyperRTS.Simulation.Navigation.Paths;
using System;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Profiling;

namespace HyperRTS.Simulation.Navigation.Drive
{
    /// <summary>
    /// Checks a vehicle can drive a fresh path and plans the stretches it can't with a <see cref="DriveSearch"/> into
    /// <see cref="PathWaypoint.Tight"/> waypoints.
    /// </summary>
    internal struct DrivePlanner : IDisposable
    {
        private const float StraightTurn = 0.01f;

        private static readonly ProfilerMarker PlanMarker = new("DrivePlanner.Plan");

        private DriveCheck _check;
        private DriveSearch _search;
        private NativeList<PathWaypoint> _tight;
        private WaypointWriter _writer;

        // Expansions this plan may spend, and whether running out defers it rather than leaving stretches as drawn.
        private int _budget;
        private bool _defers;
        private bool _planned;

        public DriveCounts Counts;

        /// <summary>The budget ran out before a search this plan needed, so it waits for a later frame.</summary>
        public bool WaitsForBudget;

        /// <summary>The budget ran out before a later stretch, left as drawn until the planned part is driven.</summary>
        public bool PartlyPlanned;

        /// <summary>A planner whose scratch serves one request after another.</summary>
        public DrivePlanner(Allocator allocator)
            : this(new DriveSearch(DriveSearch.MaxExpansions, allocator), allocator)
        {
        }

        /// <summary>A planner without a search, which only checks a path drives as drawn and has no budget.</summary>
        public static DrivePlanner WithoutSearch(Allocator allocator) => new(default(DriveSearch), allocator);

        private DrivePlanner(DriveSearch search, Allocator allocator)
        {
            _check = new DriveCheck(allocator);
            _search = search;
            _tight = new NativeList<PathWaypoint>(64, allocator);
            _writer = new WaypointWriter(allocator);
            _budget = 0;
            _defers = false;
            _planned = false;
            Counts = default;
            WaitsForBudget = false;
            PartlyPlanned = false;
        }

        public void Dispose()
        {
            if (_search.IsCreated)
            {
                _search.Dispose();
            }

            _check.Dispose();
            _tight.Dispose();
            _writer.Dispose();
        }

        /// <summary>Whether its paths need checking: it has a turn radius or a hull longer than it is wide.</summary>
        public static bool Applies(in NavAgent agent) => agent.Chassis.TurnRadius > 0f || agent.HalfSegment > 0f;

        private readonly Chassis Chassis => _check.Agent.Chassis;

        private readonly float HullLength => _check.Agent.HalfLength * 2f;

        /// <summary>
        /// Plans the drawn <paramref name="points"/> into <see cref="Waypoints"/>, planning tight stretches
        /// within <paramref name="budget"/> expansions. Past it, a plan that <paramref name="defers"/> yields nothing
        /// and is <see cref="WaitsForBudget"/>; others leave the rest as drawn. False when deferred.
        /// </summary>
        public bool Plan(in PathRequest request, in NavAccess access, NativeList<float3> points, int budget,
            bool defers)
        {
            using var scope = PlanMarker.Auto();
            if (budget > 0 && !_search.IsCreated)
            {
                throw new InvalidOperationException("A drive planner without a search has no budget to spend");
            }

            _check.Begin(request.Agent, access);
            if (_search.IsCreated)
            {
                _search.SetAgent(access, request.Agent);
            }

            Counts = default;
            return PlanPath(request, points, budget, defers);
        }

        /// <summary>The waypoints of the last <see cref="Plan"/> that finished.</summary>
        public readonly NativeArray<PathWaypoint> Waypoints => _writer.Waypoints.AsArray();

        private bool PlanPath(in PathRequest request, NativeList<float3> points, int budget, bool defers)
        {
            _writer.Begin(request.Start.y);
            _check.Start = Chassis.Pose(request.Start.xz, request.Facing);
            _budget = budget;
            _defers = defers;
            _planned = false;
            WaitsForBudget = false;
            PartlyPlanned = false;
            _check.Line.Trace(request.Start.xz, points);
            if (_check.Line.Corners.Length < 2)
            {
                foreach (var point in points)
                {
                    _writer.AddPoint(point.xz);
                }

                return true;
            }

            var cursor = PlanStart(request.Stuck);
            for (var corner = 1; corner < _check.Line.Corners.Length - 1 && !WaitsForBudget; corner++)
            {
                cursor = PlanCorner(corner, cursor);
            }

            PlanEnd();
            return !WaitsForBudget;
        }

        // The last stretch drives as drawn, so a unit ends on its goal facing whichever way it arrived.
        private void PlanEnd()
        {
            if (!WaitsForBudget)
            {
                _writer.AddDrawn(_check.Line, float.PositiveInfinity);
            }
        }

        // Wheels turn onto the path by reflex, backing to turn round, so their start is planned only once walls hold
        // them; tracks turn in place to the first leg.
        private float PlanStart(bool stuck)
        {
            var reach = math.min(_check.Line.Total, 2f * Chassis.TurnRadius + HullLength);
            if (stuck)
            {
                return Search(0f, reach);
            }

            if (Chassis.IsWheeled)
            {
                return reach;
            }

            return _check.CanStart(reach) ? 0f : Search(0f, reach);
        }

        // Sweeps the curve from a fillet and a hull before the corner to as far after; tracks may pivot on it instead.
        private float PlanCorner(int corner, float cursor)
        {
            var at = _check.Line.Distances[corner];
            var turn = _check.Line.TurnAt(corner);
            var radius = _check.Hull.CurveRadius;
            var fillet = math.min(radius * math.tan(turn * 0.5f), radius * 2f);
            if (turn < StraightTurn || at + fillet <= cursor)
            {
                return cursor;
            }

            if (!Chassis.IsWheeled && _check.CanPivot(corner))
            {
                return cursor;
            }

            var from = math.max(cursor, at - fillet - HullLength);
            var to = math.min(_check.Line.Total, at + fillet + HullLength);
            return _check.CanDrive(from, to) ? to : Search(from, to);
        }

        // Retries reaching twice as far on, where the hull may find room to turn, then stays as drawn and fails.
        private float Search(float from, float to)
        {
            var further = math.min(_check.Line.Total, to + (to - from) * 2f);
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
            _writer.BeginStretch(_check.Line, from);
            _writer.Waypoints.AddRange(_tight.AsArray());
            _writer.Written = to;
            return to;
        }

        // Out of budget, a plan that may wait does, and the frame's first plans the rest once its planned part is
        // driven, unless it planned nothing yet, which would plan the same stretch every frame.
        private void GiveUp(bool cut)
        {
            WaitsForBudget = cut && _defers;
            PartlyPlanned |= cut && !_defers && _planned;
            var leftAsDrawn = !cut || (!_defers && !_planned);
            if (leftAsDrawn)
            {
                Counts.Failures++;
            }
        }

        // Cut when what is left of the budget stopped it short of the usual cap.
        private bool TrySearch(float from, float to, int most, out bool cut)
        {
            var cap = math.min(most, _budget - (int)Counts.Expansions);
            cut = cap <= 0;
            if (cut)
            {
                return false;
            }

            Counts.Searches++;
            _tight.Clear();
            _search.Cap = cap;
            var found = _search.Run(_check.PoseAt(from), _check.GoalAt(to), _writer.Height, _tight);
            Counts.Expansions += _search.Expansions;
            cut = !found && cap < most && _search.Expansions >= cap;
            return found;
        }
    }
}
