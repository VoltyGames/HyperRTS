using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Navigation
{
    /// <summary>A unit that paths over the <see cref="NavGrid"/> and avoids neighbours within its radius.</summary>
    public struct NavAgent : IComponentData
    {
        public float Radius;
        public NavLayer Layer;

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
