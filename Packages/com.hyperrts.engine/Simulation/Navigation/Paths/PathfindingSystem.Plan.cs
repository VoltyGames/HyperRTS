using System.Collections.Generic;
using HyperRTS.Simulation.Navigation.Fields;
using HyperRTS.Simulation.Navigation.Grid;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Navigation.Paths
{
    public partial struct PathfindingSystem
    {
        /// <summary>Estimated expansions a frame's paths may cost; the frame's first request runs whatever its cost.</summary>
        public const float CostPerFrame = 30000f;

        private struct OldestFirst : IComparer<PathRequest>
        {
            public int Compare(PathRequest a, PathRequest b)
            {
                var byFrame = a.Frame.CompareTo(b.Frame);
                return byFrame != 0 ? byFrame : a.Entity.Index.CompareTo(b.Entity.Index);
            }
        }

        /// <summary>Drops stale group fields, then picks the frame's tasks oldest first until the budget is spent.</summary>
        [BurstCompile]
        private struct PlanJob : IJob
        {
            public NativeList<PathRequest> Requests;
            [ReadOnly] public NativeParallelHashSet<int2> Used;
            public bool ChecksUse;
            [ReadOnly] public NavGrid Grid;
            public GroupFields Fields;
            public PathPlan Plan;
            public double Now;

            public void Execute()
            {
                if (ChecksUse)
                {
                    Fields.DropUnused(Used);
                }

                Fields.Prune(Grid, Now);
                Requests.Sort(new OldestFirst());
                var planner = new PathPlanner(Requests.AsArray(), Grid, Fields, Plan, Now);
                planner.Run();
                planner.Dispose();
                Plan.Results.ResizeUninitialized(Plan.Tasks.Length);
                Plan.Served.ResizeUninitialized(Plan.Tasks.Length);
            }
        }
    }
}
