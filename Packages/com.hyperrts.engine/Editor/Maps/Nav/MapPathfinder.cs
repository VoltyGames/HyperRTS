using System.Collections.Generic;
using HyperRTS.Simulation.Navigation;
using Unity.Collections;
using Unity.Mathematics;

namespace HyperRTS.Editor.Maps.Nav
{
    /// <summary>
    /// The runtime's A* (<see cref="GridAStar"/>) without its budget, over ground one cell wide, so map metrics such
    /// as rush times and attack paths match in-game pathing.
    /// </summary>
    public static class MapPathfinder
    {
        /// <summary>The shortest walkable path, or null when the goal can't be reached.</summary>
        public static MapPath Find(MapNavGrid nav, int2 start, int2 goal) => Find(nav.Grid, start, goal);

        /// <summary><see cref="Find(MapNavGrid, int2, int2)"/> on a grid with its own cells, such as corridors closed.</summary>
        internal static MapPath Find(in NavGrid grid, int2 start, int2 goal)
        {
            var access = NavAccess.Surface(grid, NavLayer.Ground);
            var search = new GridAStar(grid.Cells.Length, Allocator.Persistent);
            var cells = new NativeList<int2>(256, Allocator.Persistent);
            try
            {
                var reached = search.Search(access, start, goal, int.MaxValue, cells, out var expansions);
                return reached ? Build(grid, cells, expansions) : null;
            }
            finally
            {
                cells.Dispose();
                search.Dispose();
            }
        }

        private static MapPath Build(in NavGrid grid, NativeList<int2> cells, int expansions)
        {
            var path = new List<int2>(cells.Length);
            var length = 0f;
            for (var i = 0; i < cells.Length; i++)
            {
                path.Add(cells[i]);
                length += i > 0 ? GridAStar.StepCost(cells[i - 1], cells[i]) : 0f;
            }

            return new MapPath(path, length * grid.CellSize, expansions);
        }
    }
}
