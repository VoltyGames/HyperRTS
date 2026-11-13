using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Systems;
using HyperRTS.Simulation.Common.Players;
using HyperRTS.Simulation.Movement;
using HyperRTS.Simulation.Navigation.Agents;
using HyperRTS.Simulation.Navigation.Grid;
using HyperRTS.Simulation.Navigation.Paths;
using HyperRTS.Simulation.Navigation.Spatial;
using HyperRTS.Simulation.Transport;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;

namespace HyperRTS.Simulation.Air
{
    /// <summary>
    /// Flies aircraft straight at their <see cref="MoveDestination"/> at their <see cref="Flight"/> altitude, steering round other aircraft.
    /// Idle ones that can't hover circle.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(MovementSystemGroup))]
    [UpdateAfter(typeof(PathfindingSystem))]
    [UpdateBefore(typeof(CargoFollowSystem))]
    public partial struct FlightMovementSystem : ISystem
    {
        private RightOfWay _rightOfWay;
        private Movers _shared;
        private MoverHandles _handles;
        private EntityQuery _fliers;

        public void OnCreate(ref SystemState state)
        {
            _rightOfWay = new RightOfWay(ref state);
            _shared = new Movers(ref state);
            _handles = new MoverHandles(ref state);
            _fliers = MoverHandles.Query().WithAll<Flight>().Build(ref state);
            state.RequireForUpdate<OwnerRelations>();
            state.RequireForUpdate<SpatialIndex>();
            state.RequireForUpdate<NavGrid>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var deltaTime = SystemAPI.Time.DeltaTime;
            if (deltaTime <= 0f)
            {
                return;
            }

            _rightOfWay.Update(ref state, SystemAPI.GetSingleton<OwnerRelations>());
            _shared.Update(ref state, _rightOfWay, SystemAPI.GetSingleton<SpatialIndex>(), deltaTime);
            _handles.Update(ref state);

            state.Dependency = new FlyJob
            {
                Mover = new FlightMover
                {
                    Movers = _shared,
                    Grid = SystemAPI.GetSingleton<NavGrid>(),
                    Docked = SystemAPI.GetComponentLookup<Docked>(true),
                    Calls = SystemAPI.GetComponentLookup<BoardingCall>(true),
                },
                Handles = _handles,
                Flights = SystemAPI.GetComponentTypeHandle<Flight>(true),
            }.ScheduleParallel(_fliers, state.Dependency);
        }

        // One job for aircraft with and without a LocomotionState (those without start each frame from rest).
        [BurstCompile]
        private struct FlyJob : IJobChunk
        {
            public FlightMover Mover;
            public MoverHandles Handles;
            [ReadOnly] public ComponentTypeHandle<Flight> Flights;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask,
                in v128 chunkEnabledMask)
            {
                var movers = Handles.Open(chunk);
                var flights = chunk.GetNativeArray(ref Flights);
                for (var i = 0; i < movers.Count; i++)
                {
                    Mover.Fly(movers, i, flights[i]);
                }
            }
        }
    }
}
