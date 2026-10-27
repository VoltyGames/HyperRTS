using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Navigation
{
    public partial struct PathfindingSystem
    {
        /// <summary>Estimated expansions a frame's paths may cost; the frame's first request runs whatever its cost.</summary>
        public const float CostPerFrame = 30000f;

        // A fine search clears its whole corridor's records and expands about this share of its cells.
        private const float SearchCostPerCorridorCell = 0.5f;

        // A trace walks and string-pulls its cells, with no search.
        private const float TraceCostPerCell = 2f;

        private struct OldestFirst : IComparer<PathRequest>
        {
            public int Compare(PathRequest a, PathRequest b)
            {
                var byFrame = a.Frame.CompareTo(b.Frame);
                return byFrame != 0 ? byFrame : a.Entity.Index.CompareTo(b.Entity.Index);
            }
        }

        /// <summary>Drops stale group fields, then picks the frame's tasks oldest first until the budget is spent.</summary>
        [BurstCompile]
        private struct PlanJob : IJob
        {
            public NativeList<PathRequest> Requests;
            [ReadOnly] public NativeParallelHashSet<int2> Used;
            public bool ChecksUse;
            [ReadOnly] public NavGrid Grid;
            public GroupFields Fields;
            public PathPlan Plan;
            public double Now;

            public void Execute()
            {
                if (ChecksUse)
                {
                    Fields.DropUnused(Used);
                }

                Fields.Prune(Grid, Now);
                Requests.Sort(new OldestFirst());
                var planner = new Planner(Requests.AsArray(), Grid, Fields, Plan, Now);
                planner.Run();
                planner.Dispose();
                Plan.Results.ResizeUninitialized(Plan.Tasks.Length);
            }
        }

        /// <summary>The choosing itself, with its scratch: each request's snapped query and whether it is served.</summary>
        private partial struct Planner
        {
            private NativeArray<PathRequest> _requests;
            private NavGrid _grid;
            private GroupFields _fields;
            private PathPlan _plan;
            private double _now;
            private NativeArray<PathQuery> _queries;
            private NativeArray<bool> _prepared;
            private NativeArray<bool> _handled;
            private NativeParallelMultiHashMap<int2, int> _members;
            private NativeHashSet<int2> _tried;
            private SectorSearch _search;

            // Abstract paths a split shares per start component: ranges into the routes, by component.
            private NativeList<int> _routes;
            private NativeHashMap<int, int2> _byComponent;

            public Planner(NativeArray<PathRequest> requests, in NavGrid grid, in GroupFields fields, in PathPlan plan,
                double now)
            {
                _requests = requests;
                _grid = grid;
                _fields = fields;
                _plan = plan;
                _now = now;
                _queries = new NativeArray<PathQuery>(requests.Length, Allocator.Temp);
                _prepared = new NativeArray<bool>(requests.Length, Allocator.Temp);
                _handled = new NativeArray<bool>(requests.Length, Allocator.Temp);
                _members = new NativeParallelMultiHashMap<int2, int>(math.max(1, requests.Length), Allocator.Temp);
                _tried = new NativeHashSet<int2>(8, Allocator.Temp);
                _search = new SectorSearch(Allocator.Temp);
                _routes = new NativeList<int>(256, Allocator.Temp);
                _byComponent = new NativeHashMap<int, int2>(16, Allocator.Temp);
            }

            public void Dispose()
            {
                _queries.Dispose();
                _prepared.Dispose();
                _handled.Dispose();
                _members.Dispose();
                _tried.Dispose();
                _search.Dispose();
                _routes.Dispose();
                _byComponent.Dispose();
            }

            public void Run()
            {
                for (var i = 0; i < _requests.Length; i++)
                {
                    var request = _requests[i];
                    if (request.Group != 0)
                    {
                        _members.Add(new int2(request.Group, request.Agent.Access(_grid).ClassIndex), i);
                    }
                }

                var spent = 0f;
                for (var i = 0; i < _requests.Length && (spent < CostPerFrame || _plan.Tasks.Length == 0); i++)
                {
                    spent += _handled[i] ? 0f : Choose(i);
                }
            }

            private float Choose(int index)
            {
                var request = _requests[index];
                var access = request.Agent.Access(_grid);
                var query = Query(index, access);
                if (query.Route != PathRoute.Search)
                {
                    Add(new PathTask { Request = index, Kind = PathTaskKind.Direct, Field = -1 });
                    return math.distance(query.Origin.xz, query.End.xz) / _grid.CellSize + 1f;
                }

                var opened = 0f;
                if (request.Group != 0)
                {
                    var key = new int2(request.Group, access.ClassIndex);
                    var field = _fields.Find(key, query, _grid.CellSize);
                    if (field >= 0)
                    {
                        _fields.MarkUsed(field, _now);
                        return AddTrace(index, field);
                    }

                    if (_tried.Add(key))
                    {
                        opened = OpenFields(key, access, index);
                        if (_handled[index])
                        {
                            return opened;
                        }
                    }
                }

                var first = _plan.Sectors.Length;
                _search.Find(access, query.From, query.To, _plan.Sectors);
                var count = _plan.Sectors.Length - first;
                var cost = CorridorCells(first, count) * SearchCostPerCorridorCell;
                Add(new PathTask
                {
                    Request = index, Kind = PathTaskKind.Search, Field = -1, FirstSector = first, SectorCount = count,
                    Cost = cost,
                });
                return opened + cost;
            }

            // The corridor the fine search will build round these sectors.
            private readonly int CorridorCells(int first, int count)
            {
                var corridor = new NavCorridor(_grid.Field.SectorsPerAxis, Allocator.Temp);
                corridor.AddWithRings(_plan.Sectors.AsArray().GetSubArray(first, count));

                var cells = corridor.Cells;
                corridor.Dispose();
                return cells;
            }

            private PathQuery Query(int index, in NavAccess access)
            {
                if (!_prepared[index])
                {
                    var request = _requests[index];
                    _queries[index] = GridPathfinder.Prepare(access, request.RouteStart, request.Goal);
                    _prepared[index] = true;
                }

                return _queries[index];
            }

            private float AddTrace(int index, int field)
            {
                Add(new PathTask { Request = index, Kind = PathTaskKind.Trace, Field = field });
                var query = _queries[index];
                return math.distance(query.Origin.xz, query.End.xz) / _grid.CellSize * TraceCostPerCell;
            }

            // Serves the request with its prepared query.
            private void Add(PathTask task)
            {
                _handled[task.Request] = true;
                task.Query = _queries[task.Request];
                _plan.Tasks.Add(task);
            }
        }
    }
}
