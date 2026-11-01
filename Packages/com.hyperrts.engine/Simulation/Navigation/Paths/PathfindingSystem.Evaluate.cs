using HyperRTS.Simulation.Navigation.Agents;
using HyperRTS.Simulation.Navigation.Grid;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Navigation.Paths
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
                var goalFar = math.distancesq(path.Goal.xz, goal.xz) > Grid.CellSize * Grid.CellSize;
                if (path.Status == PathStatus.Ready && path.GridVersion != Grid.Version &&
                    !path.IsNearChange(Grid, waypoints, transform.Position, agent))
                {
                    path.GridVersion = Grid.Version;
                }

                var isCurrent = path.Status == PathStatus.Ready && path.GridVersion == Grid.Version;
                if (isCurrent && path.IsRestDue(waypoints))
                {
                    path.Replan(false);
                    isCurrent = false;
                }

                var nudged = isCurrent && !goalFar;
                if (nudged && path.FollowSmallGoalMove(Grid, waypoints, goal, agent.Layer))
                {
                    return;
                }

                // A nudge the path can't follow, onto a planned manoeuvre's end, searches as a moved goal.
                var goalMoved = goalFar || nudged;

                var isChasing = isCurrent && destination.Chase;
                if (isChasing && FollowsChase(ref path, waypoints, transform.Position, goal, agent))
                {
                    return;
                }

                // Corners toward an old, distant goal would lead the wrong way while the search waits.
                // A chased goal stays near its old one, so the unit keeps closing meanwhile.
                path.Request(waypoints, Cause(path, goalMoved, destination.Chase), goalMoved && !destination.Chase,
                    Frame);
                Requests.AddNoResize(new PathRequest(entity, transform, destination, agent, path));
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
