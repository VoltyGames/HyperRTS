using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

namespace HyperRTS.Simulation.Navigation.Paths
{
    /// <summary>Adds the frame's plan and results to the running totals.</summary>
    [BurstCompile]
    internal struct PathStatsJob : IJob
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
