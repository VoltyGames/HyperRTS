using System;
using HyperRTS.Simulation.Navigation.Agents;
using HyperRTS.Simulation.Navigation.Grid;
using HyperRTS.Simulation.Navigation.Paths;
using HyperRTS.Simulation.Navigation.Sectors;
using Unity.Collections;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Tests
{
    /// <summary>The shortest route over every cell open to a hull, with no wall margin, to judge planned routes by.</summary>
    public sealed class OpenRoutes : IDisposable
    {
        private readonly NavAccess _access;
        private readonly NavGrid _grid;
        private NavCorridor _corridor;
        private GridAStar _search;
        private NativeList<int2> _path;

        public OpenRoutes(TestWorld world, in NavAgent hull)
        {
            _grid = world.Grid();
            _access = new NavAccess(_grid, hull.Access(_grid).Class, 0f);
            _corridor = NavCorridor.Whole(_grid.Field.Layout, Allocator.Persistent);
            _search = new GridAStar(_corridor, Allocator.Persistent);
            _path = new NativeList<int2>(Allocator.Persistent);
        }

        public bool IsOpen(int2 cell) => _access.IsOpen(cell);

        public int Region(int2 cell) => _access.Region(cell);

        public float ClearOf(int2 cell) => _access.Distance(cell);

        public int2 Cell(float3 position) => _grid.Grid.Cell(position);

        public float3 Point(int2 cell) => _grid.Grid.CenterPoint(cell);

        /// <summary>Metres of the shortest open route, or -1 when the start is closed or the goal out of reach.</summary>
        public float Length(float3 from, float3 to)
        {
            if (!_search.Search(_access, Cell(from), Cell(to), _path, out _))
            {
                return -1f;
            }

            var length = 0f;
            for (var i = 1; i < _path.Length; i++)
            {
                length += GridAStar.StepCost(_path[i - 1], _path[i]);
            }

            return length * _grid.Grid.CellSize;
        }

        public void Dispose()
        {
            _path.Dispose();
            _search.Dispose();
            _corridor.Dispose();
        }
    }
}
