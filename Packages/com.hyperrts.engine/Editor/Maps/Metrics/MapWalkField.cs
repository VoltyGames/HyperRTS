using System;
using System.Collections.Generic;
using Unity.Mathematics;

namespace HyperRTS.Editor.Maps.Metrics
{
    /// <summary>
    /// Walking distance from one cell to every cell of a nav grid in a single pass (8-way, the corner rule of
    /// <see cref="MapPathfinder"/>), for ranking many targets by how far they are to walk.
    /// </summary>
    public static class MapWalkField
    {
        private const float Diagonal = 1.41421356f;

        /// <summary>Metres to walk from <paramref name="start"/> to each cell by index; infinity where unreachable.</summary>
        public static float[] From(MapNavGrid nav, int2 start)
        {
            var grid = nav.Grid;
            var distance = new float[grid.Cells.Length];
            Array.Fill(distance, float.PositiveInfinity);
            var heap = new Heap();
            distance[grid.Index(start)] = 0f;
            heap.Push(0f, grid.Index(start));
            while (heap.Count > 0)
            {
                var (cost, index) = heap.Pop();
                if (cost > distance[index])
                {
                    continue;
                }

                Relax(nav, grid.Cell(index), cost, distance, heap);
            }

            for (var i = 0; i < distance.Length; i++)
            {
                distance[i] *= grid.CellSize;
            }

            return distance;
        }

        private static void Relax(MapNavGrid nav, int2 cell, float cost, float[] distance, Heap heap)
        {
            var grid = nav.Grid;
            for (var dy = -1; dy <= 1; dy++)
            {
                for (var dx = -1; dx <= 1; dx++)
                {
                    var next = cell + new int2(dx, dy);
                    var diagonal = dx != 0 && dy != 0;
                    if ((dx == 0 && dy == 0) || !CanStep(nav, cell, next, diagonal))
                    {
                        continue;
                    }

                    var nextCost = cost + (diagonal ? Diagonal : 1f);
                    var index = grid.Index(next);
                    if (nextCost < distance[index])
                    {
                        distance[index] = nextCost;
                        heap.Push(nextCost, index);
                    }
                }
            }
        }

        private static bool CanStep(MapNavGrid nav, int2 from, int2 to, bool diagonal)
        {
            if (!nav.IsWalkable(to))
            {
                return false;
            }

            return !diagonal || (nav.IsWalkable(new int2(from.x, to.y)) && nav.IsWalkable(new int2(to.x, from.y)));
        }

        /// <summary>Binary min-heap of (cost, cell index).</summary>
        private sealed class Heap
        {
            private readonly List<(float Cost, int Index)> _items = new();

            public int Count => _items.Count;

            public void Push(float cost, int index)
            {
                _items.Add((cost, index));
                var child = _items.Count - 1;
                while (child > 0)
                {
                    var parent = (child - 1) / 2;
                    if (_items[parent].Cost <= _items[child].Cost)
                    {
                        break;
                    }

                    (_items[parent], _items[child]) = (_items[child], _items[parent]);
                    child = parent;
                }
            }

            public (float Cost, int Index) Pop()
            {
                var top = _items[0];
                var last = _items[_items.Count - 1];
                _items.RemoveAt(_items.Count - 1);
                if (_items.Count > 0)
                {
                    _items[0] = last;
                    SiftDown();
                }

                return top;
            }

            private void SiftDown()
            {
                var parent = 0;
                while (true)
                {
                    var left = parent * 2 + 1;
                    var smallest = parent;
                    smallest = left < _items.Count && _items[left].Cost < _items[smallest].Cost ? left : smallest;
                    var right = left + 1;
                    smallest = right < _items.Count && _items[right].Cost < _items[smallest].Cost ? right : smallest;
                    if (smallest == parent)
                    {
                        return;
                    }

                    (_items[parent], _items[smallest]) = (_items[smallest], _items[parent]);
                    parent = smallest;
                }
            }
        }
    }
}
