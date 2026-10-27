using System;
using Unity.Collections;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Navigation
{
    /// <summary>
    /// Exact squared distance, in cells, from every cell of a square window to the nearest feature cell (Felzenszwalb
    /// and Huttenlocher). Holds its scratch, so one per worker serves many windows.
    /// </summary>
    public struct DistanceTransform : IDisposable
    {
        /// <summary>Squared distance with no feature in the window.</summary>
        public const float Far = float.MaxValue;

        /// <summary>Which window cells are walls, row by row; the caller fills it before each <see cref="Solve"/>.</summary>
        public NativeArray<bool> Closed;

        public readonly int Side;

        private NativeArray<float> _squared;
        private NativeArray<float> _line;
        private NativeArray<float> _bounds;
        private NativeArray<int> _parabolas;

        public DistanceTransform(int side, Allocator allocator)
        {
            Side = side;
            Closed = new NativeArray<bool>(side * side, allocator);
            _squared = new NativeArray<float>(side * side, allocator, NativeArrayOptions.UninitializedMemory);
            _line = new NativeArray<float>(side, allocator, NativeArrayOptions.UninitializedMemory);
            _bounds = new NativeArray<float>(side + 1, allocator, NativeArrayOptions.UninitializedMemory);
            _parabolas = new NativeArray<int>(side, allocator, NativeArrayOptions.UninitializedMemory);
        }

        public void Dispose()
        {
            Closed.Dispose();
            _squared.Dispose();
            _line.Dispose();
            _bounds.Dispose();
            _parabolas.Dispose();
        }

        /// <summary>Squared distance of a window cell from the last <see cref="Solve"/>'s features.</summary>
        public readonly float Squared(int index) => _squared[index];

        /// <summary>Measures from the closed cells when <paramref name="toClosed"/>, else from the open ones.</summary>
        public void Solve(bool toClosed)
        {
            for (var i = 0; i < _squared.Length; i++)
            {
                _squared[i] = Closed[i] == toClosed ? 0f : Far;
            }

            for (var x = 0; x < Side; x++)
            {
                Pass(x, Side);
            }

            for (var y = 0; y < Side; y++)
            {
                Pass(y * Side, 1);
            }
        }

        // One line in place: only finite parabolas join the envelope, so empty stretches never lose precision.
        private void Pass(int start, int stride)
        {
            var count = 0;
            for (var q = 0; q < Side; q++)
            {
                _line[q] = _squared[start + q * stride];
                if (_line[q] < Far)
                {
                    count = Add(q, count);
                }
            }

            if (count == 0)
            {
                return;
            }

            var k = 0;
            for (var q = 0; q < Side; q++)
            {
                while (k + 1 < count && _bounds[k + 1] < q)
                {
                    k++;
                }

                var v = _parabolas[k];
                _squared[start + q * stride] = (q - v) * (q - v) + _line[v];
            }
        }

        // Pushes the parabola rooted at q, popping those it hides; the first one's bound never pops.
        private int Add(int q, int count)
        {
            if (count == 0)
            {
                _parabolas[0] = q;
                _bounds[0] = float.NegativeInfinity;
                return 1;
            }

            var crossing = Crossing(_parabolas[count - 1], q);
            while (crossing <= _bounds[count - 1])
            {
                count--;
                crossing = Crossing(_parabolas[count - 1], q);
            }

            _parabolas[count] = q;
            _bounds[count] = crossing;
            return count + 1;
        }

        private readonly float Crossing(int v, int q) =>
            (_line[q] + q * q - (_line[v] + v * v)) / (2f * (q - v));
    }
}
