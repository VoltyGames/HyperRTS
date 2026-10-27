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
            var grid = _access.Grid;
            var span = math.abs(to - from);
            var margin = math.min(CurveRadius * 2f + HullLength, (MaxWindow - span) * 0.5f);
            _windowFirst = grid.WorldToCell(math.min(from, to) - margin);
            _windowCells = (int2)math.ceil((span + margin * 2f) / grid.CellSize) + 1;
            var halo = (int)math.ceil((HullLength * 0.5f + Clearance) / grid.CellSize) + 2;
            _walls.Fill(_access, _windowFirst - halo, _windowFirst + _windowCells + halo);
            _guide.Build(_walls, _access, _windowFirst, _windowCells, _goal, CurveRadius, HullLength);
        }

        // Ground is open where the hull centre has half a hull and a metre of room to walls.
        private readonly bool IsOpen(in AxlePose pose) =>
            _walls.At(_access.Grid.WorldToCell(_chassis.Center(pose))) >= HullLength * 0.5f + 1f;

        // The cell and heading bin in the window, or -1 outside it.
        private readonly int Key(in AxlePose pose)
        {
            var cell = _access.Grid.WorldToCell(pose.Position) - _windowFirst;
            if (math.any(cell < 0 | cell >= _windowCells))
            {
                return -1;
            }

            var turn = math.atan2(pose.Facing.y, pose.Facing.x) / (math.PI * 2f) + 1f;
            if (IsOpen(pose))
            {
                var coarse = cell / OpenCells;
                var fine = _windowCells.x * _windowCells.y * HeadingBins;
                return fine + (coarse.y * _windowCells.x + coarse.x) * OpenBins + (int)(turn * OpenBins) % OpenBins;
            }

            return (cell.y * _windowCells.x + cell.x) * HeadingBins + (int)(turn * HeadingBins) % HeadingBins;
        }

        // The guide, and for a pose goal no less than the forward curve, which keeps open-ground plans smooth.
        private readonly float Guide(in AxlePose pose)
        {
            var guide = _guide.Cost(_chassis.Center(pose), pose.Facing);
            if (!_goal.HasFacing || !DriveCurve.TryFit(pose, _goalPose, CurveRadius, out var curve))
            {
                return guide;
            }

            return math.max(guide, curve.Length);
        }
    }
}
