using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Navigation
{
    public partial struct PathfindingSystem
    {
        /// <summary>Builds the frame's new group fields, one per index.</summary>
        [BurstCompile]
        private struct FieldJob : IJobParallelForDefer
        {
            [ReadOnly] public NavGrid Grid;

            // Read only as elements: each build writes its own field's cost list.
            [ReadOnly] public NativeArray<GroupField> Fields;
            [ReadOnly] public NativeArray<int> Builds;

            public void Execute(int index)
            {
                var field = Fields[Builds[index]];
                field.Field.Build(field.Access(Grid));
            }
        }

        /// <summary>Serves one task per index, writing only that task's unit.</summary>
        [BurstCompile]
        private struct ServeJob : IJobParallelForDefer
        {
            [ReadOnly] public NativeArray<PathRequest> Requests;
            [ReadOnly] public NativeArray<PathTask> Tasks;
            [ReadOnly] public NativeArray<int> Sectors;
            [ReadOnly] public NativeArray<GroupField> Fields;
            [ReadOnly] public NavGrid Grid;
            public NativeArray<PathResult> Results;
            [NativeDisableParallelForRestriction] public BufferLookup<PathWaypoint> WaypointLookup;
            [NativeDisableParallelForRestriction] public ComponentLookup<PathState> StateLookup;
            public double Now;

            public void Execute(int index)
            {
                var task = Tasks[index];
                var request = Requests[task.Request];
                var access = request.Agent.Access(Grid);
                var points = new NativeList<float3>(16, Allocator.Temp);
                var route = Route(task, access, points);
                var result = new PathResult { Expansions = route.x, Searches = route.y };
                var waypoints = WaypointLookup[request.Entity];
                waypoints.Clear();
                var drives = task.Query.Route != PathRoute.Stay && DrivePlanner.Applies(request.Agent);
                // A path that needs a drive search leaves its drawn corners for the drive job to plan in turn.
                result.DrivePending = drives && !TryDrive(request, access, points, waypoints);
                if (!drives || result.DrivePending)
                {
                    foreach (var point in points)
                    {
                        waypoints.Add(new PathWaypoint { Position = new float3(point.x, request.Start.y, point.z) });
                    }
                }

                points.Dispose();
                Results[index] = result;
                if (!result.DrivePending)
                {
                    StateLookup[request.Entity] = ReadyState(request, task, Grid, waypoints, Now);
                }
            }

            // Writes the path when its sweeps need no search, as most do.
            private static bool TryDrive(in PathRequest request, in NavAccess access, NativeList<float3> points,
                DynamicBuffer<PathWaypoint> waypoints)
            {
                var planner = new DrivePlanner(Allocator.Temp);
                var done = planner.Plan(request, access, points, waypoints, 0, true);
                planner.Dispose();
                return done;
            }

            // Cells expanded, and 1 when a fine search ran.
            private int2 Route(in PathTask task, in NavAccess access, NativeList<float3> points)
            {
                switch (task.Kind)
                {
                    case PathTaskKind.Direct:
                        task.Query.AddDirect(points);
                        return int2.zero;
                    case PathTaskKind.Search:
                        var sectors = Sectors.GetSubArray(task.FirstSector, task.SectorCount);
                        return new int2(GridPathfinder.Route(access, task.Query, sectors, points), 1);
                    default:
                        return Trace(task, access, points);
                }
            }

            // Scenery can close a cell on the field's way without a grid version, so a broken trace searches alone.
            private int2 Trace(in PathTask task, in NavAccess access, NativeList<float3> points)
            {
                if (Fields[task.Field].Trace(Grid, task.Query, points))
                {
                    return int2.zero;
                }

                return new int2(GridPathfinder.Search(access, task.Query, points), 1);
            }
        }

        [BurstCompile]
        private struct StatsJob : IJob
        {
            [ReadOnly] public PathPlan Plan;
            [ReadOnly] public NativeArray<PathRequest> Requests;
            public NativeReference<PathStats> Stats;

            public void Execute()
            {
                var stats = Stats.Value;
                for (var i = 0; i < Plan.Tasks.Length; i++)
                {
                    var result = Plan.Results[i];
                    var task = Plan.Tasks[i];
                    stats.Expansions += result.Expansions;
                    stats.Searches += result.Searches;
                    stats.Estimated += (long)task.Cost;
                    stats.Drive.Add(result.Drive);
                    stats.DeferredDrives += result.DrivePending ? 1 : 0;
                    if (task.Kind == PathTaskKind.Search)
                    {
                        stats.SearchesByCause[(int)Requests[task.Request].Cause]++;
                    }
                    else if (task.Kind == PathTaskKind.Trace)
                    {
                        stats.Traces += 1 - result.Searches;
                        stats.Retraces += result.Searches;
                    }
                }

                stats.Fields += Plan.Builds.Length;
                Stats.Value = stats;
            }
        }
    }
}
