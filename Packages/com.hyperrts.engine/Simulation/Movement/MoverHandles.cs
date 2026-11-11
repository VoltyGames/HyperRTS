using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Players;
using HyperRTS.Simulation.Navigation.Agents;
using HyperRTS.Simulation.Navigation.Paths;
using HyperRTS.Simulation.Transport;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;

namespace HyperRTS.Simulation.Movement
{
    /// <summary>The type handles the ground and flight jobs share, which open a chunk as a <see cref="MoverChunk"/>.</summary>
    internal struct MoverHandles
    {
        [ReadOnly] private EntityTypeHandle _entities;
        private ComponentTypeHandle<LocalTransform> _transforms;
        [ReadOnly] private ComponentTypeHandle<MovementSpeed> _speeds;
        [ReadOnly] private ComponentTypeHandle<NavAgent> _agents;
        [ReadOnly] private ComponentTypeHandle<Owner> _owners;
        private ComponentTypeHandle<MoveDestination> _destinations;
        private ComponentTypeHandle<PathState> _paths;
        private BufferTypeHandle<PathWaypoint> _waypoints;
        private ComponentTypeHandle<MoveProgress> _progresses;
        private ComponentTypeHandle<Arrival> _arrivals;
        private ComponentTypeHandle<Velocity> _velocities;
        private ComponentTypeHandle<FacingGoal> _aims;
        private ComponentTypeHandle<LocomotionState> _drives;

        public MoverHandles(ref SystemState state)
        {
            _entities = state.GetEntityTypeHandle();
            _transforms = state.GetComponentTypeHandle<LocalTransform>();
            _speeds = state.GetComponentTypeHandle<MovementSpeed>(true);
            _agents = state.GetComponentTypeHandle<NavAgent>(true);
            _owners = state.GetComponentTypeHandle<Owner>(true);
            _destinations = state.GetComponentTypeHandle<MoveDestination>();
            _paths = state.GetComponentTypeHandle<PathState>();
            _waypoints = state.GetBufferTypeHandle<PathWaypoint>();
            _progresses = state.GetComponentTypeHandle<MoveProgress>();
            _arrivals = state.GetComponentTypeHandle<Arrival>();
            _velocities = state.GetComponentTypeHandle<Velocity>();
            _aims = state.GetComponentTypeHandle<FacingGoal>();
            _drives = state.GetComponentTypeHandle<LocomotionState>();
        }

        /// <summary>The components every mover has. Present, not enabled: arrival and facing are switched here, so disabled ones must still match.</summary>
        public static EntityQueryBuilder Query() => new EntityQueryBuilder(Allocator.Temp)
            .WithAllRW<LocalTransform>().WithAllRW<PathState>().WithAllRW<PathWaypoint>()
            .WithAllRW<MoveProgress>().WithAllRW<Velocity>()
            .WithAll<MovementSpeed>().WithAll<NavAgent>().WithAll<Owner>()
            .WithPresentRW<MoveDestination>().WithPresentRW<Arrival>().WithPresentRW<FacingGoal>().WithNone<Inside>();

        public MoverChunk Open(in ArchetypeChunk chunk)
        {
            var remembers = chunk.Has(ref _drives);
            return new MoverChunk
            {
                Count = chunk.Count,
                Entities = chunk.GetNativeArray(_entities),
                Transforms = chunk.GetNativeArray(ref _transforms).AsSpan(),
                Speeds = chunk.GetNativeArray(ref _speeds),
                Agents = chunk.GetNativeArray(ref _agents),
                Owners = chunk.GetNativeArray(ref _owners),
                Destinations = chunk.GetNativeArray(ref _destinations).AsSpan(),
                Paths = chunk.GetNativeArray(ref _paths).AsSpan(),
                Waypoints = chunk.GetBufferAccessorRW(ref _waypoints),
                Progresses = chunk.GetNativeArray(ref _progresses).AsSpan(),
                Arrivals = chunk.GetNativeArray(ref _arrivals).AsSpan(),
                Velocities = chunk.GetNativeArray(ref _velocities).AsSpan(),
                Aims = chunk.GetNativeArray(ref _aims).AsSpan(),
                MovingMask = chunk.GetEnabledMask(ref _destinations),
                ArrivedMask = chunk.GetEnabledMask(ref _arrivals),
                AimingMask = chunk.GetEnabledMask(ref _aims),
                Drives = remembers ? chunk.GetNativeArray(ref _drives) : default,
                Remembers = remembers,
            };
        }
    }
}
