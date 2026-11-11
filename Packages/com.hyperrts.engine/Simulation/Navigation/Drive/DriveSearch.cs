using HyperRTS.Simulation.Common;
using System;
using HyperRTS.Simulation.Navigation.Agents;
using HyperRTS.Simulation.Navigation.Fields;
using HyperRTS.Simulation.Navigation.Grid;
using HyperRTS.Simulation.Navigation.Paths;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Profiling;

namespace HyperRTS.Simulation.Navigation.Drive
{
    /// <summary>
    /// Hybrid A* over the rear axle's pose, led by a <see cref="DriveHeuristic"/>, for a stretch of path the chassis can't
    /// drive as drawn. It gives the hull centre's waypoints, each <see cref="PathWaypoint.Tight"/>.
    /// </summary>
    public struct DriveSearch : IDisposable
    {
        /// <summary>A search stops after this many expansions and leaves its stretch as drawn.</summary>
        public const int MaxExpansions = 6000;

        private const int ShortcutEvery = 10;
        private const int ShortcutNearEvery = 3;

        private static readonly ProfilerMarker RunMarker = new("DriveSearch.Run");

        private struct Node
        {
            public AxlePose Pose;

            // The pose's cell and heading bin, kept so each pop needn't work it out again.
            public int Key;

            public int Parent;
            public float Cost;
            public float Depth;

            /// <summary>Metres reversed since the last forward run of a hull length.</summary>
            public float Run;

            /// <summary>Metres driven forwards since the last reverse step.</summary>
            public float Ahead;

            public float Curvature;

            /// <summary>1 forwards, -1 in reverse, 0 at the start.</summary>
            public sbyte Gear;

            public bool Closed;
        }

        private DriveHull _hull;
        private DriveNodes _nodes;
        private DriveWindow _window;

        // The ground height the run's waypoints sit at.
        private float _height;

        /// <summary>Nodes the last <see cref="Run"/> expanded.</summary>
        public int Expansions;

        /// <summary>Expansions after which a run gives up.</summary>
        public int Cap;

        /// <summary>A search that gives up after <paramref name="cap"/> expansions, <see cref="MaxExpansions"/> in play.</summary>
        public DriveSearch(in NavAccess access, in NavAgent agent, int cap, Allocator allocator) : this(cap, allocator)
        {
            SetAgent(access, agent);
        }

        /// <summary>A search for no agent yet, whose scratch serves each one <see cref="SetAgent"/> gives it.</summary>
        public DriveSearch(int cap, Allocator allocator)
        {
            _hull = default;
            Cap = cap;
            _nodes = new DriveNodes(allocator);
            _window = new DriveWindow(allocator);
            _height = 0f;
            Expansions = 0;
        }

        /// <summary>Searches for <paramref name="agent"/> from now on; its moves follow its chassis.</summary>
        public void SetAgent(in NavAccess access, in NavAgent agent)
        {
            _hull = new DriveHull(access, agent);
            _nodes.ClearMoves();
            _window.SetAgent(access, _hull);
        }

        public readonly bool IsCreated => _nodes.IsCreated;

        public void Dispose()
        {
            _nodes.Dispose();
            _window.Dispose();
        }

        /// <summary>
        /// Searches from the rear-axle pose <paramref name="start"/> to <paramref name="goal"/> and adds the hull
        /// centre's waypoints at <paramref name="height"/>; false, adding none, when it finds no way within the cap.
        /// </summary>
        public bool Run(in AxlePose start, in DriveGoal goal, float height, NativeList<PathWaypoint> points)
        {
            using var scope = RunMarker.Auto();
            Expansions = 0;
            if (!Fits(_hull.Chassis.Center(start), goal.Center) || !CanStand(goal))
            {
                return false;
            }

            _height = height;
            Begin(start, goal);
            var found = Search(out var last, out var curve, out var curveLength);
            if (found)
            {
                Emit(last, curve, curveLength, points);
            }

            return found;
        }

        private void Begin(in AxlePose start, in DriveGoal goal)
        {
            _window.SetGoal(_hull, goal);
            _nodes.FillIfEmpty(_hull.Chassis, _hull.MostCurvature);
            _window.Fit(_hull, _hull.Chassis.Center(start), goal.Center);
            var depth = _hull.Depth(_window.Walls, start, float.PositiveInfinity);
            _nodes.Begin(start, _window.Key(_hull, start), depth, _window.Guide(_hull, start));
        }

        // Both ends on the grid and within one window, so the guide and the closed set hold them.
        private readonly bool Fits(float2 from, float2 to)
        {
            var grid = _hull.Access.Grid;
            var onGrid = grid.Grid.Contains(grid.Grid.Cell(from)) && grid.Grid.Contains(grid.Grid.Cell(to));
            return onGrid && math.all(math.abs(to - from) <= DriveWindow.MaxWindow);
        }

        // A pose goal whose hull centre sits in walls beyond the pose tolerance, as where a path runs into a building,
        // can never be reached, so it costs no expansions.
        private readonly bool CanStand(in DriveGoal goal) =>
            !goal.HasFacing || _hull.Access.Distance(goal.Center) >= _hull.Access.Class.Radius - DriveGoal.PoseReach;

        private bool Search(out int last, out DriveCurve curve, out float curveLength)
        {
            last = -1;
            curve = default;
            curveLength = 0f;
            while (_nodes.TryPop(out var index))
            {
                if (Expansions >= Cap)
                {
                    return false;
                }

                var expanded = _nodes.Close(index);
                Expansions++;
                last = index;
                if (IsAtGoal(expanded.Pose))
                {
                    return true;
                }

                if (IsShortcutDue(expanded.Pose) && TryShortcut(expanded, out curve, out curveLength))
                {
                    return true;
                }

                _nodes.Expand(index, expanded, _hull, _window);
            }

            return false;
        }

        // Every few expansions, and every one near the goal, where a curve most often fits.
        private readonly bool IsShortcutDue(in AxlePose pose)
        {
            var radius = _hull.CurveRadius;
            var near = math.distancesq(_hull.Chassis.Center(pose), _window.Goal.Center) <= 4f * radius * radius;
            return Expansions % (near ? ShortcutNearEvery : ShortcutEvery) == 1;
        }

        private readonly bool IsAtGoal(in AxlePose pose)
        {
            var center = _hull.Chassis.Center(pose);
            if (!_window.Goal.HasFacing)
            {
                return math.distance(center, _window.Goal.Center) <= NavTolerances.Arrive(_hull.Access.Class.Radius);
            }

            return _window.Goal.IsAtPose(center, pose.Facing);
        }

        private readonly bool TryShortcut(in DriveNodes.Node node, out DriveCurve curve, out float length) =>
            _hull.TryFit(node.Pose, _window.Goal, out curve, out length) &&
            _hull.IsClear(_window.Walls, curve, length, node.Depth);

        // The node chain from the start, then the shortcut, as the hull centre's waypoints.
        private readonly void Emit(int last, in DriveCurve curve, float curveLength, NativeList<PathWaypoint> points)
        {
            var first = points.Length;
            AddChain(last, points);
            for (var distance = DriveNodes.FineStep; distance < curveLength; distance += DriveNodes.FineStep)
            {
                points.Add(Waypoint(_hull.Chassis.Center(curve.Sample(distance)), false));
            }

            if (curveLength > 0f)
            {
                points.Add(Waypoint(_hull.Chassis.Center(curve.Sample(curveLength)), false));
            }

            if (_window.Goal.HasFacing)
            {
                return;
            }

            // A point goal ends on the point itself, as the drawn path did.
            if (points.Length == first)
            {
                points.Add(Waypoint(_window.Goal.Center, false));
            }

            ref var end = ref points.ElementAt(points.Length - 1);
            end.Position.xz = _window.Goal.Center;
        }

        private readonly void AddChain(int last, NativeList<PathWaypoint> points)
        {
            var first = points.Length;
            for (var i = last; _nodes[i].Parent >= 0; i = _nodes[i].Parent)
            {
                var node = _nodes[i];
                var center = _hull.Chassis.Center(node.Pose);
                // A pivot turns about the centre, which stays put.
                if (math.distancesq(center, _hull.Chassis.Center(_nodes[node.Parent].Pose)) > 1e-4f)
                {
                    points.Add(Waypoint(center, node.Gear < 0));
                }
            }

            points.ReverseFrom(first);
        }

        private readonly PathWaypoint Waypoint(float2 center, bool reverse) =>
            new() { Position = new float3(center.x, _height, center.y), Reverse = reverse, Tight = true };
    }
}
