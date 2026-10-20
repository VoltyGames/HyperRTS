using Unity.Entities;

namespace HyperRTS.Simulation.Common
{
    /// <summary>The entity being attacked. While enabled, combat owns the unit's movement (chase, stop to fire).</summary>
    public struct AttackTarget : IComponentData, IEnableableComponent
    {
        public Entity Value;

        /// <summary>
        /// Refreshed each frame by <c>EngagementSystem</c> against the longest reach of the weapons that can hit the
        /// target; each weapon fires only while set and within its own range of <see cref="Gap"/>.
        /// </summary>
        public bool InRange;

        /// <summary>Edge to edge distance to the target, refreshed with <see cref="InRange"/>.</summary>
        public float Gap;

        /// <summary>Retreating from an ordered target inside the minimum range; holds fire until clear.</summary>
        public bool BackingOff;
    }
}
