using System;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Navigation.Agents;
using HyperRTS.Simulation.Navigation.Grid;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Navigation.Fields
{
    /// <summary>
    /// One class's wall distances copied over a box of cells, so a search that reads them thousands of times reads
    /// them unchecked. Outside the box reads as wall.
    /// </summary>
    internal struct FieldWindow : IDisposable, ICellValues, IWallDistance
    {
        private UnsafeList<float> _distances;
        private CellRect _box;
        private GridFrame _frame;

        public FieldWindow(int capacity, Allocator allocator)
        {
            _distances = new UnsafeList<float>(capacity, allocator);
            _box = default;
            _frame = new GridFrame(float2.zero, 1f, int2.zero);
        }

        public void Dispose() => _distances.Dispose();

        /// <summary>Copies the cells from <paramref name="min"/> to <paramref name="max"/>, both included.</summary>
        public void Fill(in NavAccess access, int2 min, int2 max)
        {
            _box = new CellRect(min, max - min + 1);
            _frame = access.Grid.Field.Frame;
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
            return index < 0 ? _frame.CellSize * -0.5f : _distances[index];
        }

        /// <summary>Bilinear between the four nearest cell centres, as <see cref="NavField.Sample"/>.</summary>
        public readonly float Distance(float2 position) => NavField.Bilinear(this, _frame, position);

        /// <summary>
        /// How far a capsule reaches into walls, as <see cref="NavCapsule.Depth(float2, float2)"/> sums it, counted
        /// only until past <paramref name="limit"/>.
        /// </summary>
        public readonly float Depth(float2 center, float2 axis, float radius, float limit) =>
            NavCapsule.Depth(this, center, axis, radius, limit, out _);
    }
}
