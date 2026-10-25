using HyperRTS.Core;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Navigation
{
    /// <summary>
    /// Plans <see cref="PathWaypoint"/>s for units whose <see cref="MoveDestination"/> is new or moved by more than a
    /// cell, or whose path passes near cells the grid changed (<see cref="NavChanges"/>). Each frame takes requests
    /// oldest first until their estimated cost (abstract path length) spends the budget, then serves them in parallel,
    /// each to the end: none returns a partial path. Formation members share a <see cref="GroupField"/> and trace it;
    /// single moves, chases and walk-ups search a corridor. Aircraft (<see cref="NavLayer.Air"/>) fly straight.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(MovementSystemGroup))]
    [UpdateAfter(typeof(NavGridSystem))]
    public partial struct PathfindingSystem : ISystem
    {
        private EntityQuery _agents;
        private GroupFields _fields;
        private NativeReference<PathStats> _stats;
        private BufferLookup<PathWaypoint> _waypointLookup;
        private ComponentLookup<PathState> _stateLookup;
        private uint _frame;

        /// <summary>Totals of the work done so far; complete the world's jobs before reading.</summary>
        public readonly PathStats Stats => _stats.Value;

        /// <summary>The live group fields; complete the world's jobs before reading.</summary>
        public readonly GroupFields Fields => _fields;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _agents = SystemAPI.QueryBuilder()
                .WithAllRW<PathState, PathWaypoint>()
                .WithAll<NavAgent, LocalTransform>()
                .WithPresent<MoveDestination>()
                .Build();
            _fields = new GroupFields(Allocator.Persistent);
            _stats = new NativeReference<PathStats>(Allocator.Persistent);
            _waypointLookup = state.GetBufferLookup<PathWaypoint>();
            _stateLookup = state.GetComponentLookup<PathState>();
            state.RequireForUpdate<NavGrid>();
        }

        public void OnDestroy(ref SystemState state)
        {
            state.CompleteDependency();
            _fields.Dispose();
            _stats.Dispose();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _frame++;
            var grid = SystemAPI.GetSingleton<NavGrid>();
            var count = _agents.CalculateEntityCount();
            var requests = new NativeList<PathRequest>(count, Allocator.TempJob);
            var used = new NativeParallelHashSet<int2>(math.max(count, 1), Allocator.TempJob);
            state.Dependency = new EvaluateJob
            {
                Requests = requests.AsParallelWriter(),
                Used = used.AsParallelWriter(),
                Grid = grid,
                Frame = _frame,
            }.ScheduleParallel(_agents, state.Dependency);

            _waypointLookup.Update(ref state);
            _stateLookup.Update(ref state);
            var plan = new PathPlan(Allocator.TempJob);
            state.Dependency = new PlanJob
            {
                Requests = requests, Used = used, Grid = grid, Fields = _fields, Plan = plan,
                Now = SystemAPI.Time.ElapsedTime,
            }.Schedule(state.Dependency);
            state.Dependency = Serve(grid, requests, plan, state.Dependency);
            requests.Dispose(state.Dependency);
            used.Dispose(state.Dependency);
            plan.Dispose(state.Dependency);
        }

        // Fields first, as traces read them; then every task at once, each writing only its own unit.
        private JobHandle Serve(in NavGrid grid, NativeList<PathRequest> requests, in PathPlan plan,
            JobHandle dependency)
        {
            var fields = _fields.Fields.AsDeferredJobArray();
            dependency = new FieldJob { Grid = grid, Fields = fields, Builds = plan.Builds.AsDeferredJobArray() }
                .Schedule(plan.Builds, 1, dependency);
            dependency = new ServeJob
            {
                Requests = requests.AsDeferredJobArray(),
                Tasks = plan.Tasks.AsDeferredJobArray(),
                Sectors = plan.Sectors.AsDeferredJobArray(),
                Results = plan.Results.AsDeferredJobArray(),
                Fields = fields,
                Grid = grid,
                WaypointLookup = _waypointLookup,
                StateLookup = _stateLookup,
            }.Schedule(plan.Tasks, 1, dependency);
            return new StatsJob { Plan = plan, Stats = _stats }.Schedule(dependency);
        }

        /// <summary>Drops paths of stopped units and queues searches for stale ones.</summary>
        [BurstCompile]
        private partial struct EvaluateJob : IJobEntity
        {
            public NativeList<PathRequest>.ParallelWriter Requests;
            public NativeParallelHashSet<int2>.ParallelWriter Used;
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

                var classIndex = Grid.Classes.Find(agent.Class);
                if (destination.Group != 0 && classIndex >= 0)
                {
                    Used.Add(new int2(destination.Group, classIndex));
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

                Request(entity, ref path, waypoints, transform.Position, destination, agent, goalMoved);
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
                in MoveDestination destination, in NavAgent agent, bool goalMoved)
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
                    Goal = destination.Value,
                    Group = destination.Group,
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
    }
}
