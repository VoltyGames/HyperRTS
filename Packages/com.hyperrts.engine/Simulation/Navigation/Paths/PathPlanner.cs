using HyperRTS.Simulation.Navigation.Fields;
using HyperRTS.Simulation.Navigation.Grid;
using HyperRTS.Simulation.Navigation.Sectors;
using Unity.Collections;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Navigation.Paths
{
    /// <summary>
    /// Chooses the frame's path tasks oldest first until the budget is spent.
    /// </summary>
    internal struct PathPlanner
    {
        // A fine search clears its whole corridor's records and expands about this share of its cells.
        private const float SearchCostPerCorridorCell = 0.5f;

        private PathBatch _batch;
        private GroupOpener _opener;
        private NavGrid _grid;
        private GroupFields _fields;
        private PathPlan _plan;
        private double _now;
        private SectorSearch _search;
        private NavCorridor _scratch;

        public PathPlanner(NativeArray<PathRequest> requests, in NavGrid grid, in GroupFields fields, in PathPlan plan,
            double now)
        {
            _batch = new PathBatch(requests, grid, plan);
            _opener = new GroupOpener(_batch, grid, fields, plan, now);
            _grid = grid;
            _fields = fields;
            _plan = plan;
            _now = now;
            _search = new SectorSearch(Allocator.Temp);
            _scratch = new NavCorridor(grid.Field.Layout, Allocator.Temp);
        }

        public void Dispose()
        {
            _batch.Dispose();
            _opener.Dispose();
            _search.Dispose();
            _scratch.Dispose();
        }

        public void Run()
        {
            for (var i = 0; i < _batch.Count; i++)
            {
                var request = _batch.Request(i);
                if (request.Group != 0)
                {
                    _opener.AddMember(new int2(request.Group, request.Agent.Access(_grid).ClassIndex), i);
                }
            }

            var spent = 0f;
            for (var i = 0; i < _batch.Count && (spent < PathfindingSystem.CostPerFrame || _plan.Tasks.Length == 0); i++)
            {
                spent += _batch.IsHandled(i) ? 0f : Choose(i);
            }
        }

        private float Choose(int index)
        {
            var request = _batch.Request(index);
            var access = request.Agent.Access(_grid);
            var query = _batch.Query(index, access);
            if (query.Route != PathRoute.Search)
            {
                _batch.Add(new PathTask { Request = index, Kind = PathTaskKind.Direct, Field = -1 });
                return math.distance(query.Origin.xz, query.End.xz) / _grid.Grid.CellSize + 1f;
            }

            var opened = 0f;
            if (request.Group != 0)
            {
                var key = new int2(request.Group, access.ClassIndex);
                var field = _fields.Find(key, query, _grid.Grid.CellSize);
                if (field >= 0)
                {
                    _fields.MarkUsed(field, _now);
                    return _batch.AddTrace(index, field);
                }

                if (_opener.TryOpen(key, access, index, out opened) && _batch.IsHandled(index))
                {
                    return opened;
                }
            }

            var first = _plan.Sectors.Length;
            _search.Find(access, query.From, query.To, _plan.Sectors);
            var count = _plan.Sectors.Length - first;
            var cost = CorridorCells(first, count) * SearchCostPerCorridorCell;
            _batch.Add(new PathTask
            {
                Request = index, Kind = PathTaskKind.Search, Field = -1, FirstSector = first, SectorCount = count,
                Cost = cost,
            });
            return opened + cost;
        }

        // The corridor the fine search will build round these sectors.
        private int CorridorCells(int first, int count)
        {
            _scratch.AddWithRings(_plan.Sectors.AsArray().GetSubArray(first, count));
            var cells = _scratch.Cells;
            _scratch.Truncate(0);
            return cells;
        }
    }
}
