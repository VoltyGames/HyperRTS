using Unity.Mathematics;

namespace HyperRTS.Simulation.Common
{
    /// <summary>The cells of a range whose centre lies within a flat circle, row by row; see <see cref="CellGrid.CellsInCircle"/>.</summary>
    public readonly struct CellCircle
    {
        private readonly CellGrid _grid;
        private readonly CellRange _range;
        private readonly float2 _center;
        private readonly float _radius;

        public CellCircle(in CellGrid grid, CellRange range, float2 center, float radius)
        {
            _grid = grid;
            _range = range;
            _center = center;
            _radius = radius;
        }

        public Enumerator GetEnumerator() => new(_grid, _range.GetEnumerator(), _center, _radius * _radius);

        public struct Enumerator
        {
            private readonly CellGrid _grid;
            private readonly float2 _center;
            private readonly float _radiusSq;
            private CellRange.Enumerator _cells;

            public Enumerator(in CellGrid grid, CellRange.Enumerator cells, float2 center, float radiusSq)
            {
                _grid = grid;
                _cells = cells;
                _center = center;
                _radiusSq = radiusSq;
            }

            public readonly int2 Current => _cells.Current;

            public bool MoveNext()
            {
                while (_cells.MoveNext())
                {
                    if (math.distancesq(_grid.Center(_cells.Current), _center) <= _radiusSq)
                    {
                        return true;
                    }
                }

                return false;
            }
        }
    }
}
