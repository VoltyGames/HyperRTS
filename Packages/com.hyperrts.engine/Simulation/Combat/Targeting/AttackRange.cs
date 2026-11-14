using HyperRTS.Simulation.Combat.Weapons;

namespace HyperRTS.Simulation.Combat.Targeting
{
    /// <summary>One frame's measure of an engaging unit against its target, which <see cref="Engager"/> decides on.</summary>
    internal struct AttackRange
    {
        /// <summary>Edge to edge, as <see cref="TargetLookup.Gap"/>.</summary>
        public float Gap;

        /// <summary>How far the primary reaches this target from here.</summary>
        public float Reach;

        /// <summary>The primary's band widened by the extra weapons that join the attack.</summary>
        public RangeBand Band;
    }
}
