using Unity.Entities;
using Unity.Mathematics;
using Unity.Netcode;

namespace HyperRTS.Simulation.Combat.Targeting
{
    /// <summary>How an armed unit reacts to enemies it wasn't ordered to attack.</summary>
    public enum Stance : byte
    {
        /// <summary>Auto-acquire and chase targets.</summary>
        Aggressive = 0,

        /// <summary>Auto-acquire, chase a short way, then return to <see cref="CombatStance.ReturnPoint"/>.</summary>
        Defensive = 1,

        /// <summary>Fire at targets in range but never move to engage.</summary>
        HoldPosition = 2,

        /// <summary>Never auto-acquire; only attack when ordered.</summary>
        Passive = 3,
    }

    /// <summary>How an armed unit reacts to enemies when not explicitly ordered to attack.</summary>
    public struct CombatStance : IComponentData
    {
        [GhostField] public Stance Value;

        /// <summary>Where the unit was when it last became idle; defensive units return here.</summary>
        public float3 ReturnPoint;

        /// <summary>Distance at which it spots enemies when idle; 0 uses the vision range.</summary>
        public float AcquireRange;

        /// <summary>Auto-acquired targets are dropped beyond this multiple of the scan range.</summary>
        public float LeashFactor;

        public static CombatStance Create(Stance value, float3 returnPoint, float acquireRange = 0f, float leashFactor = 1.5f) => new()
        {
            Value = value,
            ReturnPoint = returnPoint,
            AcquireRange = acquireRange,
            LeashFactor = leashFactor,
        };

        /// <summary>The same settings with a new stance and post.</summary>
        public readonly CombatStance WithPost(Stance value, float3 returnPoint)
        {
            var copy = this;
            copy.Value = value;
            copy.ReturnPoint = returnPoint;
            return copy;
        }

        /// <summary>How far it looks for targets: <see cref="AcquireRange"/>, else <paramref name="visionRange"/>, never under <paramref name="weaponRange"/>.</summary>
        public readonly float ScanRange(float visionRange, float weaponRange) =>
            math.max(AcquireRange > 0f ? AcquireRange : visionRange, weaponRange);
    }
}
