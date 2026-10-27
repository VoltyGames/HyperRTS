using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Navigation
{
    public partial struct PathfindingSystem
    {
        /// <summary>
        /// Drive search expansions a frame may spend, one capped search. The frame's first path that needs a search
        /// stays within it, leaving any stretch past it as drawn; later paths that don't fit wait for the next frame.
        /// </summary>
        public const int DriveBudget = DriveSearch.MaxExpansions;

        /// <summary>
        /// Plans the paths whose sweeps found a stretch to search, one after another in task order, so the budget
        /// binds and the same paths wait every run.
        /// </summary>
        [BurstCompile]
        private struct DriveJob : IJob
        {
            [ReadOnly] public NativeArray<PathRequest> Requests;
            [ReadOnly] public NativeArray<PathTask> Tasks;
            [ReadOnly] public NavGrid Grid;
            public NativeArray<PathResult> Results;
            public BufferLookup<PathWaypoint> WaypointLookup;
            public ComponentLookup<PathState> StateLookup;
            public double Now;

            public void Execute()
            {
                var points = new NativeList<float3>(64, Allocator.Temp);
                var planned = new NativeList<PathWaypoint>(64, Allocator.Temp);
                var spent = 0;
                for (var i = 0; i < Tasks.Length && spent < DriveBudget; i++)
                {
                    if (Results[i].DrivePending)
                    {
                        spent += Plan(i, spent, points, planned);
                    }
                }

                points.Dispose();
                planned.Dispose();
            }

            // The drawn corners the serve job left in the buffer, planned within what the frame has left.
            private int Plan(int index, int spent, NativeList<float3> points, NativeList<PathWaypoint> planned)
            {
                var task = Tasks[index];
                var request = Requests[task.Request];
                var waypoints = WaypointLookup[request.Entity];
                points.Clear();
                planned.Clear();
                foreach (var waypoint in waypoints)
                {
                    points.Add(waypoint.Position);
                }

                var planner = new DrivePlanner(request.Agent.Access(Grid), request.Agent, Allocator.Temp);
                var done = planner.Plan(request, points, planned, DriveBudget - spent, spent > 0);
                var result = Results[index];
                result.DriveSearches = planner.Searches;
                result.DriveExpansions = planner.Expansions;
                result.DriveFailures = planner.Failures;
                result.DrivePending = !done;
                Results[index] = result;
                if (done)
                {
                    waypoints.Clear();
                    waypoints.AddRange(planned.AsArray());
                    var state = ReadyState(request, task, Grid, waypoints, Now);
                    state.PartlyPlanned = planner.PartlyPlanned;
                    StateLookup[request.Entity] = state;
                }

                var expansions = planner.Expansions;
                planner.Dispose();
                return expansions;
            }
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
