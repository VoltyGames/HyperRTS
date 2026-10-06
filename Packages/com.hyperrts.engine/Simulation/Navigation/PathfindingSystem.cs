using System.Collections.Generic;
using HyperRTS.Core;
using HyperRTS.Simulation.Units;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Navigation
{
    /// <summary>
    /// Plans <see cref="PathWaypoint"/>s for units whose <see cref="MoveDestination"/> is new, moved by more than a
    /// cell, or whose grid changed. Searches are capped per frame, oldest request first, to bound the frame cost.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(MovementSystemGroup))]
    [UpdateAfter(typeof(NavGridSystem))]
    public partial struct PathfindingSystem : ISystem
    {
        public const int MaxSearchesPerFrame = 48;

        private EntityQuery _agents;
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
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _frame++;
            if (!SystemAPI.TryGetSingleton<NavGrid>(out var grid) || !grid.IsCreated)
            {
                state.Dependency = new StraightPathJob().ScheduleParallel(_agents, state.Dependency);
                return;
            }

            var requests = new NativeList<PathRequest>(_agents.CalculateEntityCount(), Allocator.TempJob);
            state.Dependency = new EvaluateJob
            {
                Requests = requests.AsParallelWriter(),
                Grid = grid,
                Frame = _frame,
            }.ScheduleParallel(_agents, state.Dependency);
            state.Dependency = new SelectJob { Requests = requests }.Schedule(state.Dependency);

            _waypointLookup.Update(ref state);
            _stateLookup.Update(ref state);
            state.Dependency = new SearchJob
            {
                Requests = requests.AsDeferredJobArray(),
                Grid = grid,
                WaypointLookup = _waypointLookup,
                StateLookup = _stateLookup,
            }.Schedule(requests, 1, state.Dependency);
            requests.Dispose(state.Dependency);
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

                var goal = destination.Value;
                var goalMoved = math.distancesq(path.Goal.xz, goal.xz) > Grid.CellSize * Grid.CellSize;
                if (path.Status == PathStatus.Ready && !goalMoved && path.GridVersion == Grid.Version)
                {
                    FollowSmallGoalMove(waypoints, goal, agent.Layer);
                    return;
                }

                if (path.Status != PathStatus.Requested)
                {
                    // Corners toward an old, distant goal would lead the wrong way while the search waits.
                    if (goalMoved)
                    {
                        waypoints.Clear();
                    }

                    path.Status = PathStatus.Requested;
                    path.RequestFrame = Frame;
                }

                Requests.AddNoResize(new PathRequest
                {
                    Entity = entity,
                    Start = transform.Position,
                    Goal = goal,
                    Radius = agent.Radius,
                    Layer = agent.Layer,
                    Frame = path.RequestFrame,
                });
            }

            // Re-planning for sub-cell nudges (re-clicks, a creeping chase target) isn't worth a search.
            private void FollowSmallGoalMove(DynamicBuffer<PathWaypoint> waypoints, float3 goal, NavLayer layer)
            {
                if (waypoints.Length == 0 || !Grid.IsWalkable(goal, layer))
                {
                    return;
                }

                ref var last = ref waypoints.ElementAt(waypoints.Length - 1);
                var distanceSq = math.distancesq(last.Position.xz, goal.xz);
                if (distanceSq > 1e-6f && distanceSq <= Grid.CellSize * Grid.CellSize)
                {
                    last.Position.xz = goal.xz;
                }
            }
        }

        /// <summary>Without a grid every unit heads straight for its destination.</summary>
        [BurstCompile]
        private partial struct StraightPathJob : IJobEntity
        {
            private void Execute(ref PathState path, DynamicBuffer<PathWaypoint> waypoints,
                in MoveDestination destination, EnabledRefRO<MoveDestination> moving)
            {
                if (!moving.ValueRO)
                {
                    Reset(ref path, waypoints);
                    return;
                }

                waypoints.Clear();
                waypoints.Add(new PathWaypoint { Position = destination.Value });
                path = new PathState { Goal = destination.Value, Status = PathStatus.Ready };
            }
        }

        private static void Reset(ref PathState path, DynamicBuffer<PathWaypoint> waypoints)
        {
            if (path.Status != PathStatus.None)
            {
                path.Status = PathStatus.None;
                waypoints.Clear();
            }
        }

        /// <summary>Keeps the oldest requests up to the per-frame budget; the rest wait for a later frame.</summary>
        [BurstCompile]
        private struct SelectJob : IJob
        {
            public NativeList<PathRequest> Requests;

            public void Execute()
            {
                if (Requests.Length <= MaxSearchesPerFrame)
                {
                    return;
                }

                Requests.Sort(new OldestFirst());
                Requests.Resize(MaxSearchesPerFrame, NativeArrayOptions.UninitializedMemory);
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

        // Requests name distinct entities, so parallel writes through the lookups never collide.
        [BurstCompile]
        private struct SearchJob : IJobParallelForDefer
        {
            [ReadOnly] public NativeArray<PathRequest> Requests;
            [ReadOnly] public NavGrid Grid;
            [NativeDisableParallelForRestriction] public BufferLookup<PathWaypoint> WaypointLookup;
            [NativeDisableParallelForRestriction] public ComponentLookup<PathState> StateLookup;

            public void Execute(int index)
            {
                var request = Requests[index];
                GridPathfinder.Plan(Grid, request, WaypointLookup[request.Entity]);
                StateLookup[request.Entity] = new PathState
                {
                    Goal = request.Goal,
                    GridVersion = Grid.Version,
                    Status = PathStatus.Ready,
                };
            }
        }
    }
}
