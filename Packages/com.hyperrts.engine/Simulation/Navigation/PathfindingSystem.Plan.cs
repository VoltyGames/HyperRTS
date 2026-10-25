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
            [ReadOnly] public NavGrid Grid;
            public GroupFields Fields;
            public PathPlan Plan;
            public double Now;

            public void Execute()
            {
                Fields.Prune(Grid, Used, Now);
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
            }

            public void Dispose()
            {
                _queries.Dispose();
                _prepared.Dispose();
                _handled.Dispose();
                _members.Dispose();
                _tried.Dispose();
                _search.Dispose();
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
                _handled[index] = true;
                var request = _requests[index];
                var access = request.Agent.Access(_grid);
                var query = Query(index, access);
                if (query.Route != PathRoute.Search)
                {
                    Add(index, PathTaskKind.Direct, query);
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
                        return AddTrace(index, query, field);
                    }

                    if (_tried.Add(key))
                    {
                        opened = OpenFields(key, access, index, out var joined);
                        if (joined)
                        {
                            return opened;
                        }
                    }
                }

                var first = _plan.Sectors.Length;
                GridPathfinder.FindSectors(access, query, _plan.Sectors);
                var count = _plan.Sectors.Length - first;
                var cost = CorridorCells(first, count) * SearchCostPerCorridorCell;
                Add(index, PathTaskKind.Search, query, -1, first, count, cost);
                return opened + cost;
            }

            // The corridor the fine search will build round these sectors.
            private readonly int CorridorCells(int first, int count)
            {
                var corridor = new NavCorridor(_grid.Field.Sectors, Allocator.Temp);
                for (var i = first; i < first + count; i++)
                {
                    corridor.AddWithRing(_plan.Sectors[i]);
                }

                var cells = corridor.Cells;
                corridor.Dispose();
                return cells;
            }

            private PathQuery Query(int index, in NavAccess access)
            {
                if (!_prepared[index])
                {
                    var request = _requests[index];
                    _queries[index] = GridPathfinder.Prepare(access, request.Start, request.Goal);
                    _prepared[index] = true;
                }

                return _queries[index];
            }

            private float AddTrace(int index, in PathQuery query, int field)
            {
                Add(index, PathTaskKind.Trace, query, field);
                return math.distance(query.Origin.xz, query.End.xz) / _grid.CellSize * TraceCostPerCell;
            }

            private void Add(int index, PathTaskKind kind, in PathQuery query, int field = -1, int firstSector = 0,
                int sectorCount = 0, float cost = 0f) =>
                _plan.Tasks.Add(new PathTask
                {
                    Request = index, Kind = kind, Query = query, Field = field, FirstSector = firstSector,
                    SectorCount = sectorCount, Cost = cost,
                });
        }
    }
}
