using Unity.Collections.LowLevel.Unsafe;
using HyperRTS.Simulation.Navigation.Agents;
using HyperRTS.Simulation.Navigation.Drive;
using HyperRTS.Simulation.Navigation.Grid;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Navigation.Paths
{
    /// <summary>
    /// Gives each served unit its waypoints and, once planned, its ready state; then plans the paths whose sweeps found a stretch to search, one after another in task order, so the budget
    /// binds and the same paths wait every run.
    /// </summary>
    [BurstCompile]
    internal struct PathDriveJob : IJob
    {
        [ReadOnly] public NativeArray<PathRequest> Requests;
        [ReadOnly] public NativeArray<PathTask> Tasks;
        [ReadOnly] public NavGrid Grid;
        public NativeArray<PathResult> Results;
        public NativeArray<UnsafeList<PathWaypoint>> Served;
        public BufferLookup<PathWaypoint> WaypointLookup;
        public ComponentLookup<PathState> StateLookup;
        public double Now;

        public void Execute()
        {
            for (var i = 0; i < Tasks.Length; i++)
            {
                Store(i);
            }

            var points = new NativeList<float3>(64, Allocator.Temp);
            var planner = new DrivePlanner(Allocator.Temp);
            var spent = 0;
            for (var i = 0; i < Tasks.Length && spent < PathfindingSystem.DriveBudget; i++)
            {
                if (Results[i].DrivePending)
                {
                    spent += Plan(i, spent, points, ref planner);
                }
            }

            points.Dispose();
            planner.Dispose();
        }

        private void Store(int index)
        {
            var task = Tasks[index];
            var request = Requests[task.Request];
            var served = Served[index];
            var waypoints = WaypointLookup[request.Entity];
            waypoints.Clear();
            waypoints.EnsureCapacity(served.Length);
            foreach (var waypoint in served)
            {
                waypoints.Add(waypoint);
            }

            served.Dispose();
            if (!Results[index].DrivePending)
            {
                StateLookup[request.Entity] = ReadyState(request, task, Grid, waypoints, Now);
            }
        }

        // The drawn corners the serve job left in the buffer, planned within what the frame has left.
        private int Plan(int index, int spent, NativeList<float3> points, ref DrivePlanner planner)
        {
            var task = Tasks[index];
            var request = Requests[task.Request];
            var waypoints = WaypointLookup[request.Entity];
            points.Clear();
            foreach (var waypoint in waypoints)
            {
                points.Add(waypoint.Position);
            }

            var access = request.Agent.Access(Grid);
            var done = planner.Plan(request, access, points, PathfindingSystem.DriveBudget - spent, spent > 0);
            var result = Results[index];
            result.Drive = planner.Counts;
            result.DrivePending = !done;
            Results[index] = result;
            if (done)
            {
                waypoints.Clear();
                waypoints.AddRange(planner.Waypoints);
                var state = ReadyState(request, task, Grid, waypoints, Now);
                state.PartlyPlanned = planner.PartlyPlanned;
                StateLookup[request.Entity] = state;
            }

            return (int)planner.Counts.Expansions;
        }

        private static PathState ReadyState(in PathRequest request, in PathTask task, in NavGrid grid,
            DynamicBuffer<PathWaypoint> waypoints, double now) => new()
        {
            Goal = request.Goal,
            GridVersion = grid.Version,
            Status = PathStatus.Ready,
            IsShort = !task.Query.Exact,
            LengthPastCorner = PathState.Measure(waypoints),
            PlanTime = now,
        };
    }
}
