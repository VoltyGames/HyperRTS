using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Combat
{
    public enum Stance : byte
    {
        /// <summary>Auto-acquire and chase targets.</summary>
        Aggressive = 0,

        /// <summary>Auto-acquire, chase a short way, then return to <see cref="CombatStance.Anchor"/>.</summary>
        Defensive = 1,

        /// <summary>Fire at targets in range but never move to engage.</summary>
        HoldPosition = 2,

        /// <summary>Never auto-acquire; only attack when ordered.</summary>
        Passive = 3,
    }

    /// <summary>How an armed unit reacts to enemies when not explicitly ordered to attack.</summary>
    public struct CombatStance : IComponentData
    {
        public Stance Value;

        /// <summary>Where the unit was when it last became idle; defensive units return here.</summary>
        public float3 Anchor;
    }
}
