using Unity.Entities;

namespace HyperRTS.Simulation.Navigation
{
    /// <summary>A unit that paths over the <see cref="NavGrid"/> and avoids neighbours within its radius.</summary>
    public struct NavAgent : IComponentData
    {
        public float Radius;
    }
}
