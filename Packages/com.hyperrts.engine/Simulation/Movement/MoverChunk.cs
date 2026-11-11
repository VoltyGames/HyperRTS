using System;
using HyperRTS.Simulation.Common.Players;
using HyperRTS.Simulation.Navigation.Agents;
using HyperRTS.Simulation.Navigation.Paths;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;

namespace HyperRTS.Simulation.Movement
{
    /// <summary>One chunk's movement components, opened by <see cref="MoverHandles.Open"/> and read by index.</summary>
    internal ref struct MoverChunk
    {
        public int Count;
        public NativeArray<Entity> Entities;
        public Span<LocalTransform> Transforms;
        public NativeArray<MovementSpeed> Speeds;
        public NativeArray<NavAgent> Agents;
        public NativeArray<Owner> Owners;
        public Span<MoveDestination> Destinations;
        public Span<PathState> Paths;
        public BufferAccessor<PathWaypoint> Waypoints;
        public Span<MoveProgress> Progresses;
        public Span<Arrival> Arrivals;
        public Span<Velocity> Velocities;
        public Span<FacingGoal> Aims;
        public EnabledMask MovingMask;
        public EnabledMask ArrivedMask;
        public EnabledMask AimingMask;
        public NativeArray<LocomotionState> Drives;
        // Infantry have no LocomotionState and start each frame from rest.
        public bool Remembers;

        public EnabledRefRW<MoveDestination> Moving(int index) => MovingMask.GetEnabledRefRW<MoveDestination>(index);

        public EnabledRefRW<Arrival> Arrived(int index) => ArrivedMask.GetEnabledRefRW<Arrival>(index);

        public EnabledRefRW<FacingGoal> Aiming(int index) => AimingMask.GetEnabledRefRW<FacingGoal>(index);
    }
}
