using System.Collections.Generic;
using HyperRTS.Core;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;
using Unity.Transforms;

namespace HyperRTS.Simulation.Navigation
{
    /// <summary>
    /// Plans <see cref="PathWaypoint"/>s for units whose <see cref="MoveDestination"/> is new or moved by more than a
    /// cell, or whose path passes near cells the grid changed (<see cref="NavChanges"/>). Searches run oldest request
    /// first until the frame's budget is spent, each to the end: none returns a partial path. Aircraft
    /// (<see cref="NavLayer.Air"/>) never search: they fly straight.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(MovementSystemGroup))]
    [UpdateAfter(typeof(NavGridSystem))]
    public partial struct PathfindingSystem : ISystem
    {
        /// <summary>Cells all of a frame's searches may expand or look along; the last search started may run past it.</summary>
        public const int MaxExpansionsPerFrame = 40000;

        private EntityQuery _agents;
        private GridAStar _search;
        private BufferLookup<PathWaypoint> _waypointLookup;
        private ComponentLookup<PathState> _stateLookup;
        private uint _frame;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _agents = SystemAPI.QueryBuilder()
                .WithAllRW<PathState, PathWaypoint>()
                .WithAll<NavAgent, LocalTransform>()
                .WithPresent<MoveDestination>()
                .Build();
            _waypointLookup = state.GetBufferLookup<PathWaypoint>();
            _stateLookup = state.GetComponentLookup<PathState>();
            state.RequireForUpdate<NavGrid>();
        }

        public void OnDestroy(ref SystemState state)
        {
            state.CompleteDependency();
            if (_search.IsCreated)
            {
                _search.Dispose();
            }
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _frame++;
            var grid = SystemAPI.GetSingleton<NavGrid>();
            if (_search.Cells != grid.Cells.Length)
            {
                FitSearch(ref state, grid.Cells.Length);
            }

            var requests = new NativeList<PathRequest>(_agents.CalculateEntityCount(), Allocator.TempJob);
            state.Dependency = new EvaluateJob
            {
                Requests = requests.AsParallelWriter(),
                Grid = grid,
                Frame = _frame,
            }.ScheduleParallel(_agents, state.Dependency);

            _waypointLookup.Update(ref state);
            _stateLookup.Update(ref state);
            state.Dependency = new SearchJob
            {
                Requests = requests,
                Grid = grid,
                Search = _search,
                WaypointLookup = _waypointLookup,
                StateLookup = _stateLookup,
            }.Schedule(state.Dependency);
            requests.Dispose(state.Dependency);
        }

        // The records are sized to the grid, so a rebuilt grid of another size needs new ones.
        private void FitSearch(ref SystemState state, int cells)
        {
            if (_search.IsCreated)
            {
                state.CompleteDependency();
                _search.Dispose();
            }

            _search = new GridAStar(cells, Allocator.Persistent);
        }

        /// <summary>Drops paths of stopped units and queues searches for stale ones.</summary>
        [BurstCompile]
        private partial struct EvaluateJob : IJobEntity
        {
            public NativeList<PathRequest>.ParallelWriter Requests;
            [ReadOnly] public NavGrid Grid;
            public uint Frame;

            private void Execute(Entity entity, ref PathState path, DynamicBuffer<PathWaypoint> waypoints,
                in MoveDestination destination, EnabledRefRO<MoveDestination> moving, in LocalTransform transform,
                in NavAgent agent)
            {
                if (!moving.ValueRO)
                {
                    Reset(ref path, waypoints);
                    return;
                }

                if (agent.Layer == NavLayer.Air)
                {
                    Straight(ref path, waypoints, destination.Value);
                    return;
                }

                var goal = destination.Value;
                var goalMoved = math.distancesq(path.Goal.xz, goal.xz) > Grid.CellSize * Grid.CellSize;
                if (path.Status == PathStatus.Ready && !goalMoved && path.GridVersion != Grid.Version &&
                    !IsNearChange(path, waypoints, transform.Position, agent))
                {
                    path.GridVersion = Grid.Version;
                }

                if (path.Status == PathStatus.Ready && !goalMoved && path.GridVersion == Grid.Version)
                {
                    FollowSmallGoalMove(ref path, waypoints, goal, agent.Layer);
                    return;
                }

                Request(entity, ref path, waypoints, transform.Position, goal, agent, goalMoved);
            }

            // A change past the wall margin can neither block a leg nor make another way cheaper.
            private readonly bool IsNearChange(in PathState path, DynamicBuffer<PathWaypoint> waypoints,
                float3 position, in NavAgent agent)
            {
                if (path.IsShort || !Grid.Changes.Covers(path.GridVersion, Grid.Version))
                {
                    return true;
                }

                var margin = math.max(agent.Radius * 2f, agent.WallMargin) + Grid.CellSize;
                var from = position.xz;
                for (var i = 0; i < waypoints.Length; i++)
                {
                    var to = waypoints[i].Position.xz;
                    if (Grid.Changes.Touches(Grid, path.GridVersion, from, to, margin))
                    {
                        return true;
                    }

                    from = to;
                }

                return false;
            }

            private void Request(Entity entity, ref PathState path, DynamicBuffer<PathWaypoint> waypoints, float3 start,
                float3 goal, in NavAgent agent, bool goalMoved)
            {
                if (path.Status != PathStatus.Requested)
                {
                    // Corners toward an old, distant goal would lead the wrong way while the search waits.
                    if (goalMoved)
                    {
                        path.ClearWaypoints(waypoints);
                    }

                    path.Status = PathStatus.Requested;
                    path.RequestFrame = Frame;
                }

                Requests.AddNoResize(new PathRequest
                {
                    Entity = entity,
                    Start = start,
                    Goal = goal,
                    Agent = agent,
                    Frame = path.RequestFrame,
                });
            }

            // Re-planning for sub-cell nudges (re-clicks, a creeping chase target) isn't worth a search.
            private void FollowSmallGoalMove(ref PathState path, DynamicBuffer<PathWaypoint> waypoints, float3 goal,
                NavLayer layer)
            {
                if (waypoints.Length == 0 || !Grid.IsWalkable(goal, layer))
                {
                    return;
                }

                var distanceSq = math.distancesq(waypoints[waypoints.Length - 1].Position.xz, goal.xz);
                if (distanceSq > 1e-6f && distanceSq <= Grid.CellSize * Grid.CellSize)
                {
                    path.MoveLast(waypoints, goal.xz);
                }
            }
        }

        private static void Straight(ref PathState path, DynamicBuffer<PathWaypoint> waypoints, float3 goal)
        {
            waypoints.Clear();
            waypoints.Add(new PathWaypoint { Position = goal });
            path = new PathState { Goal = goal, Status = PathStatus.Ready };
        }

        private static void Reset(ref PathState path, DynamicBuffer<PathWaypoint> waypoints)
        {
            if (path.Status != PathStatus.None)
            {
                path.Status = PathStatus.None;
                path.ClearWaypoints(waypoints);
            }
        }

        private struct OldestFirst : IComparer<PathRequest>
        {
            public int Compare(PathRequest a, PathRequest b)
            {
                var byFrame = a.Frame.CompareTo(b.Frame);
                return byFrame != 0 ? byFrame : a.Entity.Index.CompareTo(b.Entity.Index);
            }
        }

        /// <summary>Serves the oldest requests until the frame's budget is spent; the rest wait for a later frame.</summary>
        [BurstCompile]
        private struct SearchJob : IJob
        {
            private static readonly ProfilerMarker SearchMarker = new("PathfindingSystem.Search");

            public NativeList<PathRequest> Requests;
            [ReadOnly] public NavGrid Grid;
            public GridAStar Search;
            public BufferLookup<PathWaypoint> WaypointLookup;
            public ComponentLookup<PathState> StateLookup;

            public void Execute()
            {
                Requests.Sort(new OldestFirst());
                var work = 0;
                for (var i = 0; i < Requests.Length && work < MaxExpansionsPerFrame; i++)
                {
                    Serve(Requests[i], ref work);
                }
            }

            private void Serve(in PathRequest request, ref int work)
            {
                using var scope = SearchMarker.Auto();
                var access = request.Agent.Access(Grid);
                var waypoints = WaypointLookup[request.Entity];
                var exact = GridPathfinder.Plan(ref Search, access, request.Start, request.Goal, waypoints, ref work);

                StateLookup[request.Entity] = new PathState
                {
                    Goal = request.Goal,
                    GridVersion = Grid.Version,
                    Status = PathStatus.Ready,
                    IsShort = !exact,
                    LengthPastCorner = PathState.Measure(waypoints),
                };
            }
        }
    }
}
