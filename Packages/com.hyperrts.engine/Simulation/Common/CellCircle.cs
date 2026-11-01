using Unity.Mathematics;

namespace HyperRTS.Simulation.Common
{
    /// <summary>The cells of a range whose centre lies within a flat circle, row by row; see <see cref="GridFrame.CellsInCircle"/>.</summary>
    public readonly struct CellCircle
    {
        private readonly GridFrame _frame;
        private readonly CellRange _range;
        private readonly float2 _center;
        private readonly float _radius;

        public CellCircle(in GridFrame frame, CellRange range, float2 center, float radius)
        {
            _frame = frame;
            _range = range;
            _center = center;
            _radius = radius;
        }

        public Enumerator GetEnumerator() => new(_frame, _range.GetEnumerator(), _center, _radius * _radius);

        public struct Enumerator
        {
            private readonly GridFrame _frame;
            private readonly float2 _center;
            private readonly float _radiusSq;
            private CellRange.Enumerator _cells;

            public Enumerator(in GridFrame frame, CellRange.Enumerator cells, float2 center, float radiusSq)
            {
                _frame = frame;
                _cells = cells;
                _center = center;
                _radiusSq = radiusSq;
            }

            public readonly int2 Current => _cells.Current;

            public bool MoveNext()
            {
                while (_cells.MoveNext())
                {
                    if (math.distancesq(_frame.Center(_cells.Current), _center) <= _radiusSq)
                    {
                        return true;
                    }
                }

                return false;
            }
        }
    }
}
