using HyperRTS.Simulation.Navigation.Grid;
using Unity.Collections;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Navigation.Paths
{
    /// <summary>A frame's requests with each one's snapped query and whether a task serves it yet.</summary>
    internal struct PathBatch
    {
        // A trace walks and string-pulls its cells, with no search.
        private const float TraceCostPerCell = 2f;

        private NativeArray<PathRequest> _requests;
        private NavGrid _grid;
        private PathPlan _plan;
        private NativeArray<PathQuery> _queries;
        private NativeArray<bool> _prepared;
        private NativeArray<bool> _handled;

        public PathBatch(NativeArray<PathRequest> requests, in NavGrid grid, in PathPlan plan)
        {
            _requests = requests;
            _grid = grid;
            _plan = plan;
            _queries = new NativeArray<PathQuery>(requests.Length, Allocator.Temp);
            _prepared = new NativeArray<bool>(requests.Length, Allocator.Temp);
            _handled = new NativeArray<bool>(requests.Length, Allocator.Temp);
        }

        public readonly int Count => _requests.Length;

        public void Dispose()
        {
            _queries.Dispose();
            _prepared.Dispose();
            _handled.Dispose();
        }

        public readonly PathRequest Request(int index) => _requests[index];

        public readonly bool IsHandled(int index) => _handled[index];

        public PathQuery Query(int index, in NavAccess access)
        {
            if (!_prepared[index])
            {
                var request = _requests[index];
                _queries[index] = GridPathfinder.Prepare(access, request.Start, request.Goal);
                _prepared[index] = true;
            }

            return _queries[index];
        }

        public float AddTrace(int index, int field)
        {
            Add(new PathTask { Request = index, Kind = PathTaskKind.Trace, Field = field });
            var query = _queries[index];
            return math.distance(query.Origin.xz, query.End.xz) / _grid.Grid.CellSize * TraceCostPerCell;
        }

        // Serves the request with its prepared query.
        public void Add(PathTask task)
        {
            _handled[task.Request] = true;
            task.Query = _queries[task.Request];
            _plan.Tasks.Add(task);
        }
    }
}
