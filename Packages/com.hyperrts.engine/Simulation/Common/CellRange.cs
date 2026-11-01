using Unity.Mathematics;

namespace HyperRTS.Simulation.Common
{
    /// <summary>The cells of a box from <see cref="Min"/> to <see cref="Max"/> inclusive, row by row; empty when Min passes Max.</summary>
    public readonly struct CellRange
    {
        public readonly int2 Min;
        public readonly int2 Max;

        public CellRange(int2 min, int2 max)
        {
            Min = min;
            Max = max;
        }

        public Enumerator GetEnumerator() => new(Min, Max);

        public struct Enumerator
        {
            private readonly int2 _min;
            private readonly int2 _max;
            private int2 _cell;

            public Enumerator(int2 min, int2 max)
            {
                _min = min;
                _max = max;
                _cell = new int2(min.x - 1, min.y);
            }

            public readonly int2 Current => _cell;

            public bool MoveNext()
            {
                _cell.x++;
                if (_cell.x > _max.x)
                {
                    _cell = new int2(_min.x, _cell.y + 1);
                }

                return _cell.y <= _max.y && _min.x <= _max.x;
            }
        }
    }
}
