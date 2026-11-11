using System;
using HyperRTS.Simulation.Common.Players;
using HyperRTS.Simulation.Navigation.Agents;
using HyperRTS.Simulation.Navigation.Paths;
using HyperRTS.Simulation.Navigation.Spatial;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
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

        /// <summary>The mover as its neighbours see it in the spatial index.</summary>
        public SpatialEntry Entry(int index, bool moving)
        {
            var agent = Agents[index];
            var transform = Transforms[index];
            var position = transform.Position.xz;
            var facing = math.normalizesafe(transform.Forward().xz);
            var entry = new SpatialEntry
            {
                Entity = Entities[index],
                Position = new float3(position.x, 0f, position.y),
                Radius = agent.Radius,
                Owner = Owners[index].Value,
                IsUnit = true,
                IsMoving = moving,
                Group = Destinations[index].Group,
                Facing = facing,
                Heading = moving ? math.normalizesafe(Destinations[index].Value.xz - position) : float2.zero,
                Velocity = Velocities[index].Value,
                Layer = agent.Layer,
                SegmentAxis = facing * agent.HalfSegment,
                Spacing = agent.Spacing,
                Mass = agent.Mass,
                IsVehicle = agent.IsVehicle,
            };
            entry.FixReach();
            return entry;
        }

        public EnabledRefRW<MoveDestination> Moving(int index) => MovingMask.GetEnabledRefRW<MoveDestination>(index);

        public EnabledRefRW<Arrival> Arrived(int index) => ArrivedMask.GetEnabledRefRW<Arrival>(index);

        public EnabledRefRW<FacingGoal> Aiming(int index) => AimingMask.GetEnabledRefRW<FacingGoal>(index);
    }
}
