using System;
using HyperRTS.Simulation.Navigation;
using Unity.Mathematics;

namespace HyperRTS.Editor.Maps.Nav
{
    /// <summary>
    /// Walking distance from one cell to every cell of a nav grid in a single pass, by the step rules of
    /// <see cref="GridAStar"/>, for ranking many targets by how far they are to walk.
    /// </summary>
    public static class MapWalkField
    {
        /// <summary>Metres to walk from <paramref name="start"/> to each cell by index; infinity where unreachable.</summary>
        public static float[] From(MapNavGrid nav, int2 start)
        {
            var grid = nav.Grid;
            var distance = new float[grid.Cells.Length];
            Array.Fill(distance, float.PositiveInfinity);
            var heap = new MapHeap();
            var access = nav.Access;
            distance[grid.Index(start)] = 0f;
            heap.Push(0f, grid.Index(start));
            while (heap.Count > 0)
            {
                var (cost, index) = heap.Pop();
                if (cost > distance[index])
                {
                    continue;
                }

                Relax(access, grid.Cell(index), cost, distance, heap);
            }

            for (var i = 0; i < distance.Length; i++)
            {
                distance[i] *= grid.CellSize;
            }

            return distance;
        }

        private static void Relax(in NavAccess access, int2 cell, float cost, float[] distance, MapHeap heap)
        {
            for (var dy = -1; dy <= 1; dy++)
            {
                for (var dx = -1; dx <= 1; dx++)
                {
                    var next = cell + new int2(dx, dy);
                    if ((dx == 0 && dy == 0) || !GridAStar.CanStep(access, cell, next))
                    {
                        continue;
                    }

                    var nextCost = cost + GridAStar.Cost(access, cell, next);
                    var index = access.Grid.Index(next);
                    if (nextCost < distance[index])
                    {
                        distance[index] = nextCost;
                        heap.Push(nextCost, index);
                    }
                }
            }
        }
    }
}
