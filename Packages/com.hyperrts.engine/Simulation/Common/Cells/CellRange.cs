using Unity.Mathematics;

namespace HyperRTS.Simulation.Common.Cells
{
    /// <summary>
    /// The cells of a box from <see cref="Min"/> to <see cref="Max"/> inclusive, row by row; empty when Min passes Max.
    /// Cells are also numbered row by row from 0, for arrays laid over the box.
    /// </summary>
    public readonly struct CellRange
    {
        public readonly int2 Min;
        public readonly int2 Max;

        public CellRange(int2 min, int2 max)
        {
            Min = min;
            Max = max;
        }

        /// <summary>The box <paramref name="size"/> cells across from <paramref name="min"/>.</summary>
        public static CellRange Sized(int2 min, int2 size) => new(min, min + size - 1);

        public int2 Size => Max - Min + 1;

        public int Count => Size.x * Size.y;

        /// <summary>The cell's number in the box, or -1 outside it.</summary>
        public int Index(int2 cell)
        {
            var local = cell - Min;
            return math.any(local < 0 | local >= Size) ? -1 : local.y * Size.x + local.x;
        }

        public int2 Cell(int index) => Min + new int2(index % Size.x, index / Size.x);

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
