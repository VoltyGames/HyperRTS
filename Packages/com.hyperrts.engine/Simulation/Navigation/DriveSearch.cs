using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using Unity.Profiling;

namespace HyperRTS.Simulation.Navigation
{
    /// <summary>
    /// Hybrid A* over the rear axle's pose, led by a <see cref="DriveGuide"/>, for a stretch of path the chassis can't
    /// drive as drawn. It gives the hull centre's waypoints, each <see cref="PathWaypoint.Tight"/>.
    /// </summary>
    public partial struct DriveSearch : IDisposable
    {
        /// <summary>A search stops after this many expansions and leaves its stretch as drawn.</summary>
        public const int MaxExpansions = 6000;

        private const int ShortcutEvery = 10;
        private const int ShortcutNearEvery = 3;
        private const float PoseReach = 0.75f;
        private static readonly float PoseAngleCos = math.cos(math.radians(10f));

        private static readonly ProfilerMarker RunMarker = new("DriveSearch.Run");

        private struct Node
        {
            public AxlePose Pose;
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

        private readonly NavAccess _access;
        private readonly Chassis _chassis;
        private readonly float _halfSegment;

        // Unchecked, as they are the inner loop of every search.
        private UnsafeList<Node> _nodes;
        private UnsafeList<Move> _moves;
        private OpenSet _open;

        // The best node found so far for each cell and heading bin.
        private UnsafeHashMap<int, int> _visited;
        private FieldWindow _walls;
        private DriveGuide _guide;
        private DriveGoal _goal;
        private AxlePose _goalPose;
        private CellRect _window;

        // The ground height the run's waypoints sit at.
        private float _height;

        /// <summary>Nodes the last <see cref="Run"/> expanded.</summary>
        public int Expansions;

        /// <summary>Expansions after which a run gives up.</summary>
        public int Cap;

        /// <summary>A search that gives up after <paramref name="cap"/> expansions, <see cref="MaxExpansions"/> in play.</summary>
        public DriveSearch(in NavAccess access, in NavAgent agent, int cap, Allocator allocator)
        {
            _access = access;
            _chassis = agent.Chassis;
            _halfSegment = agent.HalfSegment;
            Cap = cap;
            _nodes = new UnsafeList<Node>(4096, allocator);
            _moves = new UnsafeList<Move>(24, allocator);
            _open = new OpenSet(4096, allocator);
            _visited = new UnsafeHashMap<int, int>(4096, allocator);
            _walls = new FieldWindow(16384, allocator);
            _guide = default;
            _goal = default;
            _goalPose = default;
            _window = default;
            _height = 0f;
            Expansions = 0;
            _guide = new DriveGuide(access, CurveRadius, HullLength, 16384, allocator);
        }

        public void Dispose()
        {
            _nodes.Dispose();
            _moves.Dispose();
            _open.Dispose();
            _visited.Dispose();
            _walls.Dispose();
            _guide.Dispose();
        }

        /// <summary>
        /// Searches from the rear-axle pose <paramref name="start"/> to <paramref name="goal"/> and adds the hull
        /// centre's waypoints at <paramref name="height"/>; false, adding none, when it finds no way within the cap.
        /// </summary>
        public bool Run(in AxlePose start, in DriveGoal goal, float height, NativeList<PathWaypoint> points)
        {
            using var scope = RunMarker.Auto();
            Expansions = 0;
            if (!Fits(_chassis.Center(start), goal.Center) || !CanStand(goal))
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
            _goal = goal;
            _goalPose = _chassis.Pose(goal.Center, goal.Facing);
            _nodes.Clear();
            _open.Clear();
            _visited.Clear();
            if (_moves.IsEmpty)
            {
                AddMoves();
            }

            SetWindow(_chassis.Center(start), goal.Center);
            _nodes.Add(new Node { Pose = start, Parent = -1, Depth = HullDepth(start, float.PositiveInfinity) });
            _visited[Key(start)] = 0;
            _open.Push(0, Guide(start));
        }

        // Both ends on the grid and within one window, so the guide and the closed set hold them.
        private readonly bool Fits(float2 from, float2 to)
        {
            var grid = _access.Grid;
            var onGrid = grid.InBounds(grid.WorldToCell(from)) && grid.InBounds(grid.WorldToCell(to));
            return onGrid && math.all(math.abs(to - from) <= MaxWindow);
        }

        // A pose goal whose hull centre sits in walls beyond the pose tolerance, as where a path runs into a building,
        // can never be reached, so it costs no expansions.
        private readonly bool CanStand(in DriveGoal goal) =>
            !goal.HasFacing || _access.Distance(goal.Center) >= _access.Class.Radius - PoseReach;

        private bool Search(out int last, out DriveCurve curve, out float curveLength)
        {
            last = -1;
            curve = default;
            curveLength = 0f;
            while (_open.TryPop(out var index))
            {
                ref var node = ref _nodes.ElementAt(index);
                if (node.Closed || _visited[Key(node.Pose)] != index)
                {
                    continue;
                }

                if (Expansions >= Cap)
                {
                    return false;
                }

                node.Closed = true;
                Expansions++;
                last = index;
                var expanded = node;
                if (IsAtGoal(expanded.Pose))
                {
                    return true;
                }

                if (IsShortcutDue(expanded.Pose) && TryShortcut(expanded, out curve, out curveLength))
                {
                    return true;
                }

                Expand(index, expanded);
            }

            return false;
        }

        // Every few expansions, and every one near the goal, where a curve most often fits.
        private readonly bool IsShortcutDue(in AxlePose pose)
        {
            var near = math.distancesq(_chassis.Center(pose), _goal.Center) <= 4f * CurveRadius * CurveRadius;
            return Expansions % (near ? ShortcutNearEvery : ShortcutEvery) == 1;
        }

        private readonly bool IsAtGoal(in AxlePose pose)
        {
            var center = _chassis.Center(pose);
            if (!_goal.HasFacing)
            {
                return math.distance(center, _goal.Center) <= NavTolerances.Arrive(_access.Class.Radius);
            }

            return math.distance(center, _goal.Center) <= PoseReach && math.dot(pose.Facing, _goal.Facing) >= PoseAngleCos;
        }

        // The node chain from the start, then the shortcut, as the hull centre's waypoints.
        private readonly void Emit(int last, in DriveCurve curve, float curveLength, NativeList<PathWaypoint> points)
        {
            var first = points.Length;
            AddChain(last, points);
            for (var distance = FineStep; distance < curveLength; distance += FineStep)
            {
                points.Add(Waypoint(_chassis.Center(curve.Sample(distance)), false));
            }

            if (curveLength > 0f)
            {
                points.Add(Waypoint(_chassis.Center(curve.Sample(curveLength)), false));
            }

            if (_goal.HasFacing)
            {
                return;
            }

            // A point goal ends on the point itself, as the drawn path did.
            if (points.Length == first)
            {
                points.Add(Waypoint(_goal.Center, false));
            }

            ref var end = ref points.ElementAt(points.Length - 1);
            end.Position.xz = _goal.Center;
        }

        private readonly void AddChain(int last, NativeList<PathWaypoint> points)
        {
            var first = points.Length;
            for (var i = last; _nodes[i].Parent >= 0; i = _nodes[i].Parent)
            {
                var node = _nodes[i];
                var center = _chassis.Center(node.Pose);
                // A pivot turns about the centre, which stays put.
                if (math.distancesq(center, _chassis.Center(_nodes[node.Parent].Pose)) > 1e-4f)
                {
                    points.Add(Waypoint(center, node.Gear < 0));
                }
            }

            for (int a = first, b = points.Length - 1; a < b; a++, b--)
            {
                var swapped = points[a];
                points[a] = points[b];
                points[b] = swapped;
            }
        }

        private readonly PathWaypoint Waypoint(float2 center, bool reverse) =>
            new() { Position = new float3(center.x, _height, center.y), Reverse = reverse, Tight = true };
    }
}
