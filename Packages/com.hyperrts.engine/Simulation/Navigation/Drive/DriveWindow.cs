using HyperRTS.Simulation.Common.Cells;
using HyperRTS.Simulation.Navigation.Fields;
using HyperRTS.Simulation.Navigation.Grid;
using Unity.Collections;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Navigation.Drive
{
    /// <summary>The patch of grid one drive search covers: its wall distances, its guide toward the goal and its pose keys.</summary>
    internal struct DriveWindow
    {
        public const float MaxWindow = 128f;
        private const int HeadingBins = 72;

        // Open ground keys on cells this many times wider and on this many heading bins, so it holds fewer poses.
        private const int OpenCells = 2;
        private const int OpenBins = 24;

        private FieldWindow _walls;
        private DriveHeuristic _heuristic;
        private CellRange _range;

        public DriveGoal Goal;
        public AxlePose GoalPose;

        public DriveWindow(Allocator allocator)
        {
            _walls = new FieldWindow(16384, allocator);
            _heuristic = new DriveHeuristic(16384, allocator);
            _range = default;
            Goal = default;
            GoalPose = default;
        }

        public readonly FieldWindow Walls => _walls;

        public void SetAgent(in NavAccess access, in DriveHull hull) =>
            _heuristic.SetAgent(access, hull.CurveRadius, hull.HullLength);

        public void Dispose()
        {
            _walls.Dispose();
            _heuristic.Dispose();
        }

        public void SetGoal(in DriveHull hull, in DriveGoal goal)
        {
            Goal = goal;
            GoalPose = hull.Chassis.Pose(goal.Center, goal.Facing);
        }

        // The stretch's ends plus room for two turns and a hull, trimmed to keep the window at most MaxWindow across.
        public void Fit(in DriveHull hull, float2 from, float2 to)
        {
            var grid = hull.Access.Grid;
            var span = math.abs(to - from);
            var margin = math.min(hull.CurveRadius * 2f + hull.HullLength, (MaxWindow - span) * 0.5f);
            var first = grid.Grid.Cell(math.min(from, to) - margin);
            _range = CellRange.Sized(first, (int2)math.ceil((span + margin * 2f) / grid.Grid.CellSize) + 1);
            var halo = (int)math.ceil((hull.HullLength * 0.5f + DriveHull.Clearance) / grid.Grid.CellSize) + 2;
            _walls.Fill(hull.Access, first - halo, first + _range.Size + halo);
            _heuristic.Build(_walls, _range, Goal);
        }

        // Ground is open where the hull centre has half a hull and a metre of room to walls.
        public readonly bool IsOpen(in DriveHull hull, in AxlePose pose) =>
            _walls.At(hull.Access.Grid.Grid.Cell(hull.Chassis.Center(pose))) >= hull.HullLength * 0.5f + 1f;

        // The cell and heading bin in the window, or -1 outside it.
        public readonly int Key(in DriveHull hull, in AxlePose pose)
        {
            var cell = hull.Access.Grid.Grid.Cell(pose.Position);
            var index = _range.Index(cell);
            if (index < 0)
            {
                return -1;
            }

            var turn = math.atan2(pose.Facing.y, pose.Facing.x) / (math.PI * 2f) + 1f;
            if (IsOpen(hull, pose))
            {
                var coarse = (cell - _range.Min) / OpenCells;
                var fine = _range.Count * HeadingBins;
                return fine + (coarse.y * _range.Size.x + coarse.x) * OpenBins + (int)(turn * OpenBins) % OpenBins;
            }

            return index * HeadingBins + (int)(turn * HeadingBins) % HeadingBins;
        }

        // The guide, and for a pose goal no less than the forward curve, which keeps open-ground plans smooth.
        public readonly float Guide(in DriveHull hull, in AxlePose pose)
        {
            var guide = _heuristic.Cost(hull.Chassis.Center(pose), pose.Facing);
            if (!Goal.HasFacing || !DriveCurve.TryFit(pose, GoalPose, hull.CurveRadius, out var curve))
            {
                return guide;
            }

            return math.max(guide, curve.Length);
        }
    }
}
