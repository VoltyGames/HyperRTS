using Unity.Collections.LowLevel.Unsafe;
using HyperRTS.Simulation.Navigation.Agents;
using HyperRTS.Simulation.Navigation.Fields;
using HyperRTS.Simulation.Navigation.Grid;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Navigation.Paths
{
    /// <summary>Serves one task per index, keeping its waypoints at that index of <see cref="PathPlan.Served"/>.</summary>
    [BurstCompile]
    internal struct PathServeJob : IJobParallelForDefer
    {
        [ReadOnly] public NativeArray<PathRequest> Requests;
        [ReadOnly] public NativeArray<PathTask> Tasks;
        [ReadOnly] public NativeArray<int> Sectors;
        [ReadOnly] public NativeArray<GroupField> Fields;
        [ReadOnly] public NavGrid Grid;
        public NativeArray<PathResult> Results;
        public NativeArray<UnsafeList<PathWaypoint>> Served;

        public void Execute(int index)
        {
            var task = Tasks[index];
            var request = Requests[task.Request];
            var access = request.Agent.Access(Grid);
            var points = new NativeList<float3>(16, Allocator.Temp);
            var route = Route(task, access, points);
            var result = new PathResult { Expansions = route.x, Searches = route.y };
            var reverses = request.DrivesBackward(points);
            var path = new UnsafeList<PathWaypoint>(points.Length, Allocator.TempJob);
            foreach (var point in points)
            {
                path.Add(new PathWaypoint { Position = new float3(point.x, request.Start.y, point.z), Reverse = reverses });
            }

            Served[index] = path;
            points.Dispose();
            Results[index] = result;
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
}
