using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Navigation
{
    /// <summary>
    /// One class's wall distances copied over a box of cells, so a search that reads them thousands of times reads
    /// them unchecked. Outside the box reads as wall.
    /// </summary>
    internal struct FieldWindow : IDisposable
    {
        private UnsafeList<float> _distances;
        private CellRect _box;
        private float2 _origin;
        private float _cellSize;

        public FieldWindow(int capacity, Allocator allocator)
        {
            _distances = new UnsafeList<float>(capacity, allocator);
            _box = default;
            _origin = float2.zero;
            _cellSize = 1f;
        }

        public void Dispose() => _distances.Dispose();

        /// <summary>Copies the cells from <paramref name="min"/> to <paramref name="max"/>, both included.</summary>
        public void Fill(in NavAccess access, int2 min, int2 max)
        {
            _box = new CellRect(min, max - min + 1);
            _origin = access.Grid.Field.Min;
            _cellSize = access.Grid.Field.CellSize;
            _distances.Resize(_box.Count);
            for (var i = 0; i < _box.Count; i++)
            {
                _distances[i] = access.Distance(_box.Cell(i));
            }
        }

        /// <summary>The value at a cell centre.</summary>
        public readonly float At(int2 cell)
        {
            var index = _box.Index(cell);
            return index < 0 ? _cellSize * -0.5f : _distances[index];
        }

        /// <summary>Bilinear between the four nearest cell centres, as <see cref="NavField.Sample"/>.</summary>
        public readonly float Sample(float2 position)
        {
            var p = (position - _origin) / _cellSize - 0.5f;
            var cell = (int2)math.floor(p);
            var t = p - cell;
            var low = math.lerp(At(cell), At(cell + new int2(1, 0)), t.x);
            var high = math.lerp(At(cell + new int2(0, 1)), At(cell + 1), t.x);
            return math.lerp(low, high, t.y);
        }

        /// <summary>
        /// How far a capsule reaches into walls, as <see cref="NavCapsule.Depth(float2, float2)"/> sums it, counted
        /// only until past <paramref name="limit"/>.
        /// </summary>
        public readonly float Depth(float2 center, float2 axis, float radius, float limit)
        {
            var points = NavCapsule.Points(axis, radius);
            var reach = NavCapsule.Reach(axis, points);
            var depth = 0f;
            for (var i = 0; i < points && depth <= limit;)
            {
                var room = Sample(NavCapsule.Point(center, axis, i, points)) - radius;
                depth -= math.min(room, 0f);
                i += room >= 0f ? 1 + (int)math.min(room / reach, points) : 1;
            }

            return depth;
        }
    }
}
