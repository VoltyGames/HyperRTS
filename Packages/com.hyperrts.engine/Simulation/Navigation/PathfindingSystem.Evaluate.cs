using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Navigation
{
    public partial struct PathfindingSystem
    {
        /// <summary>Drops paths of stopped units and queues searches for stale ones.</summary>
        [BurstCompile]
        private partial struct EvaluateJob : IJobEntity
        {
            public NativeList<PathRequest>.ParallelWriter Requests;
            public NativeParallelHashSet<int2>.ParallelWriter Used;
            public bool ChecksUse;
            [ReadOnly] public NavGrid Grid;
            public uint Frame;
            public double Now;

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
                if (ChecksUse && destination.Group != 0 && classIndex >= 0)
                {
                    Used.Add(new int2(destination.Group, classIndex));
                }

                var goal = destination.Value;
                var goalMoved = math.distancesq(path.Goal.xz, goal.xz) > Grid.CellSize * Grid.CellSize;
                if (path.Status == PathStatus.Ready && path.GridVersion != Grid.Version &&
                    !IsNearChange(path, waypoints, transform.Position, agent))
                {
                    path.GridVersion = Grid.Version;
                }

                var isCurrent = path.Status == PathStatus.Ready && path.GridVersion == Grid.Version;
                if (isCurrent && path.IsRestDue(waypoints))
                {
                    path.Replan(false);
                    isCurrent = false;
                }

                if (isCurrent && !goalMoved)
                {
                    if (FollowSmallGoalMove(ref path, waypoints, goal, agent.Layer))
                    {
                        return;
                    }

                    goalMoved = true;
                }

                var isChasing = isCurrent && destination.Chase;
                if (isChasing && FollowsChase(ref path, waypoints, transform.Position, goal, agent))
                {
                    return;
                }

                Request(entity, ref path, waypoints, transform, destination, agent, goalMoved);
            }

            // Between searches a chase keeps its path, re-aimed by sight where it can be.
            private readonly bool FollowsChase(ref PathState path, DynamicBuffer<PathWaypoint> waypoints,
                float3 position, float3 goal, in NavAgent agent)
            {
                if (waypoints.Length == 0)
                {
                    return false;
                }

                return path.TryFollowGoal(agent.Access(Grid), position, goal, waypoints) || !path.IsChaseSearchDue(Now);
            }

            // A dropped path recorded why it was dropped.
            private static PathCause Cause(in PathState path, bool goalMoved, bool chase)
            {
                if (path.Status == PathStatus.None)
                {
                    return path.Cause;
                }

                if (goalMoved)
                {
                    return chase ? PathCause.Chase : PathCause.Order;
                }

                return PathCause.GridChange;
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

            private void Request(Entity entity, ref PathState path, DynamicBuffer<PathWaypoint> waypoints,
                in LocalTransform transform, in MoveDestination destination, in NavAgent agent, bool goalMoved)
            {
                if (path.Status != PathStatus.Requested)
                {
                    path.Cause = Cause(path, goalMoved, destination.Chase);
                    // Corners toward an old, distant goal would lead the wrong way while the search waits.
                    // A chased goal stays near its old one, so the unit keeps closing meanwhile.
                    if (goalMoved && !destination.Chase)
                    {
                        path.ClearWaypoints(waypoints);
                    }

                    path.Status = PathStatus.Requested;
                    path.RequestFrame = Frame;
                }

                Requests.AddNoResize(new PathRequest
                {
                    Entity = entity,
                    Start = transform.Position,
                    Facing = math.normalizesafe(transform.Forward().xz),
                    Goal = destination.Value,
                    GoalFacing = destination.Facing,
                    RunIn = destination.RunIn,
                    ExitRun = destination.ExitRun,
                    Group = destination.Group,
                    Agent = agent,
                    Frame = path.RequestFrame,
                    Cause = path.Cause,
                    Stuck = path.Stuck,
                });
            }

            // Re-planning for sub-cell nudges (re-clicks, a creeping chase target) isn't worth a search, but the end
            // of a planned manoeuvre never moves; false when it would, so it searches again.
            private readonly bool FollowSmallGoalMove(ref PathState path, DynamicBuffer<PathWaypoint> waypoints,
                float3 goal, NavLayer layer)
            {
                if (waypoints.Length == 0 || !Grid.IsWalkable(goal, layer))
                {
                    return true;
                }

                var last = waypoints[waypoints.Length - 1];
                var distanceSq = math.distancesq(last.Position.xz, goal.xz);
                if (distanceSq <= 1e-6f || distanceSq > Grid.CellSize * Grid.CellSize)
                {
                    return true;
                }

                if (last.Tight)
                {
                    return false;
                }

                path.MoveLast(waypoints, goal.xz);
                return true;
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
                path.Drop(waypoints);
            }
        }
    }
}
