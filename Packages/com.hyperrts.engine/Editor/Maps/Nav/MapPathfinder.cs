using System.Collections.Generic;
using HyperRTS.Simulation.Navigation;
using Unity.Collections;
using Unity.Mathematics;

namespace HyperRTS.Editor.Maps.Nav
{
    /// <summary>
    /// The runtime's A* (<see cref="GridAStar"/>) as <see cref="MapNavGrid.ToolsClass"/>, with no wall margin, so map
    /// metrics such as rush times and attack paths match in-game pathing by length. It searches the whole map, not a
    /// corridor, so lengths stay the true shortest.
    /// </summary>
    public static class MapPathfinder
    {
        /// <summary>The shortest walkable path, or null when the goal can't be reached.</summary>
        public static MapPath Find(MapNavGrid nav, int2 start, int2 goal) => Find(nav.Access, start, goal);

        /// <summary><see cref="Find(MapNavGrid, int2, int2)"/> on a grid with its own cells, such as corridors closed.</summary>
        internal static MapPath Find(in NavAccess access, int2 start, int2 goal)
        {
            var region = access.Region(start);
            if (region == 0 || access.Region(goal) != region)
            {
                return null;
            }

            var corridor = NavCorridor.Whole(access.Grid.Field.Sectors, Allocator.Persistent);
            var search = new GridAStar(corridor.Cells, Allocator.Persistent);
            var cells = new NativeList<int2>(256, Allocator.Persistent);
            try
            {
                search.Search(access, corridor, start, goal, cells, out var expansions);
                return Build(access.Grid, cells, expansions);
            }
            finally
            {
                cells.Dispose();
                search.Dispose();
                corridor.Dispose();
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
