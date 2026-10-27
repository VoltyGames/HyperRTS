using Unity.Collections;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Navigation
{
    /// <summary>The field round one cell, read once, so a search tests steps to its eight neighbours from it.</summary>
    public struct CellBlock
    {
        // Row by row from the cell below-left; the centre is 4.
        private FixedList64Bytes<float> _distances;
        private readonly float _open;
        private readonly float _wallMargin;

        public CellBlock(in NavAccess access, int2 cell)
        {
            _distances = new FixedList64Bytes<float>();
            for (var dy = -1; dy <= 1; dy++)
            {
                for (var dx = -1; dx <= 1; dx++)
                {
                    _distances.Add(access.Distance(cell + new int2(dx, dy)));
                }
            }

            _open = access.OpenDistance;
            _wallMargin = access.WallMargin;
        }

        public readonly bool IsOpen(int2 offset) => _distances[(offset.y + 1) * 3 + offset.x + 1] >= _open;

        /// <summary>Whether the step from the centre to the neighbour is allowed.</summary>
        public readonly bool CanStepOut(int2 offset) => IsOpen(offset) && IsCornerOpen(offset);

        /// <summary>Whether the step from the neighbour onto the centre is allowed.</summary>
        public readonly bool CanStepIn(int2 offset) => IsOpen(offset) && IsOpen(int2.zero) && IsCornerOpen(offset);

        /// <summary>What the step from the centre to the neighbour costs.</summary>
        public readonly float CostOut(int2 offset) =>
            GridAStar.StepCost(int2.zero, offset) *
            NavAccess.CellCost(_distances[(offset.y + 1) * 3 + offset.x + 1], _wallMargin);

        /// <summary>What the step from the neighbour onto the centre costs.</summary>
        public readonly float CostIn(int2 offset) =>
            GridAStar.StepCost(int2.zero, offset) * NavAccess.CellCost(_distances[4], _wallMargin);

        // A diagonal needs both cells beside it open, so it never cuts a corner.
        private readonly bool IsCornerOpen(int2 offset) =>
            offset.x == 0 || offset.y == 0 || (IsOpen(new int2(offset.x, 0)) && IsOpen(new int2(0, offset.y)));
    }
}
