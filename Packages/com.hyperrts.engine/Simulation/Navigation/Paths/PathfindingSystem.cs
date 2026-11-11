using HyperRTS.Simulation.Common.Systems;
using HyperRTS.Simulation.Navigation.Agents;
using HyperRTS.Simulation.Navigation.Drive;
using HyperRTS.Simulation.Navigation.Fields;
using HyperRTS.Simulation.Navigation.Grid;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Navigation.Paths
{
    /// <summary>
    /// Plans <see cref="PathWaypoint"/>s for units with a new move or a path the grid changed near, a frame's budget at
    /// a time and each path whole. Formation members trace a shared <see cref="GroupField"/>.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(MovementSystemGroup))]
    [UpdateAfter(typeof(NavGridSystem))]
    public partial struct PathfindingSystem : ISystem
    {
        /// <summary>
        /// Drive search expansions a frame may spend, one capped search. The frame's first path that needs a search
        /// stays within it, leaving any stretch past it as drawn; later paths that don't fit wait for the next frame.
        /// </summary>
        public const int DriveBudget = DriveSearch.MaxExpansions;

        /// <summary>Estimated expansions a frame's paths may cost; the frame's first request runs whatever its cost.</summary>
        public const float CostPerFrame = 30000f;

        private const double UseCheckSeconds = 1.0;

        private EntityQuery _agents;
        private GroupFields _fields;
        private NativeReference<PathStats> _stats;
        private uint _frame;
        private double _nextUseCheck;

        /// <summary>Totals of the work done so far; complete the world's jobs before reading.</summary>
        public readonly PathStats Stats => _stats.Value;

        /// <summary>The live group fields; complete the world's jobs before reading.</summary>
        public readonly GroupFields Fields => _fields;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _agents = SystemAPI.QueryBuilder()
                .WithAllRW<PathState, PathWaypoint>()
                .WithAll<NavAgent, LocalTransform>()
                .WithPresent<MoveDestination>()
                .Build();
            _fields = new GroupFields(Allocator.Persistent);
            _stats = new NativeReference<PathStats>(Allocator.Persistent);
            state.RequireForUpdate<NavGrid>();
        }

        public void OnDestroy(ref SystemState state)
        {
            state.CompleteDependency();
            _fields.Dispose();
            _stats.Dispose();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _frame++;
            var grid = SystemAPI.GetSingleton<NavGrid>();
            var count = _agents.CalculateEntityCount();
            var now = SystemAPI.Time.ElapsedTime;
            // Which group fields moving units still have is gathered about once a second, not every frame.
            var checksUse = now >= _nextUseCheck;
            if (checksUse)
            {
                _nextUseCheck = now + UseCheckSeconds;
            }

            var requests = new NativeList<PathRequest>(count, Allocator.TempJob);
            var used = new NativeParallelHashSet<int2>(checksUse ? math.max(count, 1) : 1, Allocator.TempJob);
            state.Dependency = new PathEvaluateJob
            {
                Requests = requests.AsParallelWriter(),
                Used = used.AsParallelWriter(),
                ChecksUse = checksUse,
                Grid = grid,
                Frame = _frame,
                Now = now,
            }.ScheduleParallel(_agents, state.Dependency);

            var plan = new PathPlan(Allocator.TempJob);
            state.Dependency = new PathPlanJob
            {
                Requests = requests, Used = used, ChecksUse = checksUse, Grid = grid, Fields = _fields, Plan = plan,
                Now = now,
            }.Schedule(state.Dependency);
            state.Dependency = Serve(ref state, grid, requests, plan, now, state.Dependency);
            requests.Dispose(state.Dependency);
            used.Dispose(state.Dependency);
            plan.Dispose(state.Dependency);
        }

        // Fields first, as traces read them; then every task at once, each keeping its waypoints for the drive job to store.
        private JobHandle Serve(ref SystemState state, in NavGrid grid, NativeList<PathRequest> requests,
            in PathPlan plan, double now, JobHandle dependency)
        {
            var fields = _fields.Fields.AsDeferredJobArray();
            dependency = new PathFieldJob { Grid = grid, Fields = fields, Builds = plan.Builds.AsDeferredJobArray() }
                .Schedule(plan.Builds, 1, dependency);
            dependency = new PathServeJob
            {
                Requests = requests.AsDeferredJobArray(),
                Tasks = plan.Tasks.AsDeferredJobArray(),
                Sectors = plan.Sectors.AsDeferredJobArray(),
                Results = plan.Results.AsDeferredJobArray(),
                Fields = fields,
                Grid = grid,
                Served = plan.Served.AsDeferredJobArray(),
            }.Schedule(plan.Tasks, 1, dependency);
            dependency = new PathDriveJob
            {
                Requests = requests.AsDeferredJobArray(),
                Tasks = plan.Tasks.AsDeferredJobArray(),
                Results = plan.Results.AsDeferredJobArray(),
                Served = plan.Served.AsDeferredJobArray(),
                Grid = grid,
                WaypointLookup = SystemAPI.GetBufferLookup<PathWaypoint>(false),
                StateLookup = SystemAPI.GetComponentLookup<PathState>(false),
                Now = now,
            }.Schedule(dependency);
            return new PathStatsJob { Plan = plan, Requests = requests.AsDeferredJobArray(), Stats = _stats }
                .Schedule(dependency);
        }
    }
}
