using Unity.Mathematics;

namespace HyperRTS.Simulation.Common.Cells
{
    /// <summary>
    /// Walks the cells a straight line crosses on a <see cref="CellGrid"/>, in order and on or off the grid (Amanatides
    /// and Woo). Each cell comes with the shares of the line, 0 at the start and 1 at the end, where it enters and
    /// leaves; a line through a corner steps diagonally, skipping the two cells beside it.
    /// </summary>
    public struct GridRay
    {
        private readonly int2 _step;
        private readonly float2 _span;
        private float2 _next;
        private int _crossings;
        private int2 _cell;
        private bool _started;

        public GridRay(in CellGrid grid, float2 from, float2 to)
        {
            var start = grid.Local(from);
            var delta = grid.Local(to) - start;
            _cell = (int2)math.floor(start);
            _crossings = math.csum(math.abs((int2)math.floor(start + delta) - _cell));
            _step = (int2)math.sign(delta);

            // The line's share per cell crossed, and where it next crosses each axis.
            var still = delta == 0f;
            _span = math.select(1f / math.abs(delta), float.PositiveInfinity, still);
            var toEdge = math.select((float2)_cell + 1f - start, start - _cell, delta < 0f);
            _next = math.select(toEdge * _span, float.PositiveInfinity, still);
            _started = false;
            Entry = 0f;
            Exit = 0f;
            CrossedCorner = false;
        }

        public readonly int2 Cell => _cell;

        /// <summary>The sign of the line's direction on each axis, the step to the next cell.</summary>
        public readonly int2 Step => _step;

        public float Entry { get; private set; }

        public float Exit { get; private set; }

        /// <summary>Whether the line entered this cell through a corner, from the cell diagonally behind it.</summary>
        public bool CrossedCorner { get; private set; }

        /// <summary>Moves to the next cell; the first call yields the start cell.</summary>
        public bool MoveNext()
        {
            if (!_started)
            {
                _started = true;
                Exit = math.min(math.cmin(_next), 1f);
                return true;
            }

            if (_crossings <= 0)
            {
                return false;
            }

            var axis = new bool2(_next.x <= _next.y, _next.y <= _next.x);
            CrossedCorner = math.all(axis);
            Entry = Exit;
            _cell += math.select(int2.zero, _step, axis);
            _next += math.select(float2.zero, _span, axis);
            _crossings -= CrossedCorner ? 2 : 1;
            Exit = math.min(math.cmin(_next), 1f);
            return true;
        }
    }
}
