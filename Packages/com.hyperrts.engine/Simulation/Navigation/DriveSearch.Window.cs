using Unity.Mathematics;

namespace HyperRTS.Simulation.Navigation
{
    public partial struct DriveSearch
    {
        private const float MaxWindow = 128f;
        private const int HeadingBins = 72;

        // Open ground keys on cells this many times wider and on this many heading bins, so it holds fewer poses.
        private const int OpenCells = 2;
        private const int OpenBins = 24;

        // The stretch's ends plus room for two turns and a hull, trimmed to keep the window at most MaxWindow across.
        private void SetWindow(float2 from, float2 to)
        {
            var grid = _hull.Access.Grid;
            var span = math.abs(to - from);
            var margin = math.min(_hull.CurveRadius * 2f + _hull.HullLength, (MaxWindow - span) * 0.5f);
            var first = grid.WorldToCell(math.min(from, to) - margin);
            _window = new CellRect(first, (int2)math.ceil((span + margin * 2f) / grid.CellSize) + 1);
            var halo = (int)math.ceil((_hull.HullLength * 0.5f + DriveHull.Clearance) / grid.CellSize) + 2;
            _walls.Fill(_hull.Access, first - halo, first + _window.Cells + halo);
            _guide.Build(_walls, _window, _goal);
        }

        // Ground is open where the hull centre has half a hull and a metre of room to walls.
        private readonly bool IsOpen(in AxlePose pose) =>
            _walls.At(_hull.Access.Grid.WorldToCell(_hull.Chassis.Center(pose))) >= _hull.HullLength * 0.5f + 1f;

        // The cell and heading bin in the window, or -1 outside it.
        private readonly int Key(in AxlePose pose)
        {
            var cell = _hull.Access.Grid.WorldToCell(pose.Position);
            var index = _window.Index(cell);
            if (index < 0)
            {
                return -1;
            }

            var turn = math.atan2(pose.Facing.y, pose.Facing.x) / (math.PI * 2f) + 1f;
            if (IsOpen(pose))
            {
                var coarse = (cell - _window.First) / OpenCells;
                var fine = _window.Count * HeadingBins;
                return fine + (coarse.y * _window.Cells.x + coarse.x) * OpenBins + (int)(turn * OpenBins) % OpenBins;
            }

            return index * HeadingBins + (int)(turn * HeadingBins) % HeadingBins;
        }

        // The guide, and for a pose goal no less than the forward curve, which keeps open-ground plans smooth.
        private readonly float Guide(in AxlePose pose)
        {
            var guide = _guide.Cost(_hull.Chassis.Center(pose), pose.Facing);
            if (!_goal.HasFacing || !DriveCurve.TryFit(pose, _goalPose, _hull.CurveRadius, out var curve))
            {
                return guide;
            }

            return math.max(guide, curve.Length);
        }
    }
}
