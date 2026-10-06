using Unity.Entities;

namespace HyperRTS.Simulation.Navigation
{
    /// <summary>A unit that paths over the <see cref="NavGrid"/> and avoids neighbours within its radius.</summary>
    public struct NavAgent : IComponentData
    {
        public float Radius;
        public NavLayer Layer;

        /// <summary>Entities without an agent (buildings, props) sit on the ground.</summary>
        public static NavLayer LayerOf(in ComponentLookup<NavAgent> agents, Entity entity) =>
            agents.TryGetComponent(entity, out var agent) ? agent.Layer : NavLayer.Ground;
    }
}
