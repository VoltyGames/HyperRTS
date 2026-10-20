using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Navigation
{
    /// <summary>
    /// A unit that paths over the <see cref="NavGrid"/> and keeps <see cref="Spacing"/> from its neighbours. Its
    /// footprint is a circle of <see cref="Radius"/>, or a capsule <see cref="HullLength"/> long along its forward.
    /// </summary>
    public struct NavAgent : IComponentData
    {
        public float Radius;

        /// <summary>Full footprint length along the forward; up to the diameter keeps it round.</summary>
        public float HullLength;

        /// <summary>Gap kept clear between this footprint and its neighbours'.</summary>
        public float Spacing;

        public NavLayer Layer;

        /// <summary>Half the capsule's core segment: zero for round footprints.</summary>
        public readonly float HalfSegment => math.max(0f, HullLength * 0.5f - Radius);

        /// <summary>Half the footprint's length; the radius for round footprints.</summary>
        public readonly float HalfLength => Radius + HalfSegment;

        /// <summary>
        /// Formation slot pitch across (x) and along (y) the travel direction: a quarter footprint of slack plus the
        /// spacing, so a column of long hulls doesn't pile up nose to tail.
        /// </summary>
        public readonly float2 SlotSpacing =>
            new float2(Radius * 2f, HalfLength * 2f) * 1.25f + Spacing;

        /// <summary>The core segment's half on the XZ plane, along the unit's facing.</summary>
        public readonly float2 SegmentAxis(quaternion rotation) =>
            math.normalizesafe(math.mul(rotation, math.forward()).xz) * HalfSegment;

        /// <summary>Entities without an agent (buildings, props) sit on the ground.</summary>
        public static NavLayer GetLayer(in ComponentLookup<NavAgent> agents, Entity entity) =>
            agents.TryGetComponent(entity, out var agent) ? agent.Layer : NavLayer.Ground;
    }

    /// <summary>Locomotion goal on the XZ plane: set and enable it to move, disabled again on arrival.</summary>
    public struct MoveDestination : IComponentData, IEnableableComponent
    {
        public float3 Value;
    }

    /// <summary>Remaining corners of a unit's path, nearest first.</summary>
    [InternalBufferCapacity(8)]
    public struct PathWaypoint : IBufferElementData
    {
        public float3 Position;
    }
}
