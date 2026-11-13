using Unity.Collections.LowLevel.Unsafe;
using HyperRTS.Simulation.Navigation.Agents;
using HyperRTS.Simulation.Navigation.Grid;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace HyperRTS.Simulation.Navigation.Paths
{
    /// <summary>Gives each served unit its waypoints and its ready state.</summary>
    [BurstCompile]
    internal struct PathStoreJob : IJob
    {
        [ReadOnly] public NativeArray<PathRequest> Requests;
        [ReadOnly] public NativeArray<PathTask> Tasks;
        [ReadOnly] public NavGrid Grid;
        public NativeArray<UnsafeList<PathWaypoint>> Served;
        public BufferLookup<PathWaypoint> WaypointLookup;
        public ComponentLookup<PathState> StateLookup;
        public double Now;

        public void Execute()
        {
            for (var i = 0; i < Tasks.Length; i++)
            {
                var task = Tasks[i];
                var request = Requests[task.Request];
                var served = Served[i];
                var waypoints = WaypointLookup[request.Entity];
                waypoints.Clear();
                waypoints.EnsureCapacity(served.Length);
                foreach (var waypoint in served)
                {
                    waypoints.Add(waypoint);
                }

                served.Dispose();
                StateLookup[request.Entity] = new PathState
                {
                    Goal = request.Goal,
                    GridVersion = Grid.Version,
                    Status = PathStatus.Ready,
                    IsShort = !task.Query.Exact,
                    LengthPastCorner = PathState.Measure(waypoints),
                    PlanTime = Now,
                };
            }
        }
    }
}
