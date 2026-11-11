using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Infantry;
using HyperRTS.Simulation.Common.Players;
using HyperRTS.Simulation.Navigation.Agents;
using HyperRTS.Simulation.Navigation.Grid;
using HyperRTS.Simulation.Navigation.Paths;
using HyperRTS.Simulation.Navigation.Spatial;
using HyperRTS.Simulation.Transport;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Profiling;
using Unity.Transforms;

namespace HyperRTS.Simulation.Movement
{
    /// <summary>
    /// Moves ground and naval units along their <see cref="PathWaypoint"/>s toward an enabled
    /// <see cref="MoveDestination"/>, and when the move ends by itself disables it and enables <see cref="Arrival"/>.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(MovementSystemGroup))]
    [UpdateAfter(typeof(PathfindingSystem))]
    [UpdateBefore(typeof(CargoFollowSystem))]
    public partial struct MovementSystem : ISystem
    {
        private static readonly ProfilerMarker MoveMarker = new("MovementSystem.Move");

        private RightOfWay _rightOfWay;
        private EntityQuery _movers;

        public void OnCreate(ref SystemState state)
        {
            _rightOfWay = new RightOfWay(ref state);
            // Present, not enabled: arrival and facing are switched here, so disabled ones must still match.
            _movers = new EntityQueryBuilder(Allocator.Temp)
                .WithAllRW<LocalTransform>().WithAllRW<PathState>().WithAllRW<PathWaypoint>()
                .WithAllRW<MoveProgress>().WithAllRW<Velocity>()
                .WithAll<MovementSpeed>().WithAll<NavAgent>().WithAll<Owner>()
                .WithPresentRW<MoveDestination>().WithPresentRW<Arrival>().WithPresentRW<FacingGoal>().WithNone<Inside>().Build(ref state);
            state.RequireForUpdate<OwnerRelations>();
            state.RequireForUpdate<SpatialIndex>();
            state.RequireForUpdate<NavGrid>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _rightOfWay.Update(ref state, SystemAPI.GetSingleton<OwnerRelations>());
            var index = SystemAPI.GetSingleton<SpatialIndex>();
            var grid = SystemAPI.GetSingleton<NavGrid>();
            // A paused frame moves nothing and has no velocity to measure.
            if (SystemAPI.Time.DeltaTime <= 0f)
            {
                return;
            }

            state.Dependency = new MoveJob
            {
                Mover = new GroundMover
                {
                    Movers = new Movers
                    {
                        Index = index,
                        RightOfWay = _rightOfWay,
                        Locomotions = SystemAPI.GetComponentLookup<Locomotion>(true),
                        DeltaTime = SystemAPI.Time.DeltaTime,
                    },
                    Grid = grid,
                    Paces = SystemAPI.GetComponentLookup<Pace>(true),
                },
                Handles = new MoverHandles(ref state),
            }.ScheduleParallel(_movers, state.Dependency);
        }

        // One job for units with and without a LocomotionState (infantry start each frame from rest).
        [BurstCompile]
        private struct MoveJob : IJobChunk
        {
            public GroundMover Mover;
            public MoverHandles Handles;

            // Per chunk, not per unit, so the marker costs little and the profiler stays readable.
            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask,
                in v128 chunkEnabledMask)
            {
                MoveMarker.Begin();
                var movers = Handles.Open(chunk);
                for (var i = 0; i < movers.Count; i++)
                {
                    Mover.Move(movers, i);
                }

                MoveMarker.End();
            }
        }
    }
}
