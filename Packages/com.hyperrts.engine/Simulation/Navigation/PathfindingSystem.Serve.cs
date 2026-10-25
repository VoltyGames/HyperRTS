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
            public NativeArray<int2> Results;
            [NativeDisableParallelForRestriction] public BufferLookup<PathWaypoint> WaypointLookup;
            [NativeDisableParallelForRestriction] public ComponentLookup<PathState> StateLookup;

            public void Execute(int index)
            {
                var task = Tasks[index];
                var request = Requests[task.Request];
                var points = new NativeList<float3>(16, Allocator.Temp);
                Results[index] = Route(task, request.Agent.Access(Grid), points);

                var waypoints = WaypointLookup[request.Entity];
                waypoints.Clear();
                foreach (var point in points)
                {
                    waypoints.Add(new PathWaypoint { Position = new float3(point.x, request.Start.y, point.z) });
                }

                points.Dispose();
                StateLookup[request.Entity] = new PathState
                {
                    Goal = request.Goal,
                    GridVersion = Grid.Version,
                    Status = PathStatus.Ready,
                    IsShort = !task.Query.Exact,
                    LengthPastCorner = PathState.Measure(waypoints),
                };
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

                var sectors = new NativeList<int>(32, Allocator.Temp);
                GridPathfinder.FindSectors(access, task.Query, sectors);
                var expansions = GridPathfinder.Route(access, task.Query, sectors.AsArray(), points);
                sectors.Dispose();
                return new int2(expansions, 1);
            }
        }

        [BurstCompile]
        private struct StatsJob : IJob
        {
            [ReadOnly] public PathPlan Plan;
            public NativeReference<PathStats> Stats;

            public void Execute()
            {
                var stats = Stats.Value;
                for (var i = 0; i < Plan.Tasks.Length; i++)
                {
                    var result = Plan.Results[i];
                    stats.Expansions += result.x;
                    stats.Searches += result.y;
                    stats.Traces += Plan.Tasks[i].Kind == PathTaskKind.Trace && result.y == 0 ? 1 : 0;
                }

                stats.Fields += Plan.Builds.Length;
                Stats.Value = stats;
            }
        }
    }
}
