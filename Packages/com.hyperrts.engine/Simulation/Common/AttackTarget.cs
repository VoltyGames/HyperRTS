using Unity.Entities;

namespace HyperRTS.Simulation.Common
{
    /// <summary>The entity being attacked. While enabled, combat owns the unit's movement (chase, stop to fire).</summary>
    public struct AttackTarget : IComponentData, IEnableableComponent
    {
        public Entity Value;

        /// <summary>Refreshed each frame by <c>EngagementSystem</c>; the weapon fires only while set.</summary>
        public bool InRange;
    }
}
