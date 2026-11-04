using HyperRTS.Simulation.Combat.Weapons;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Infantry;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Combat.Damage
{
    /// <summary>What one hit deals: its damage and type, the share that reaches a garrison and its splash.</summary>
    public struct DamageProfile
    {
        /// <summary>Damage before armor; negative heals.</summary>
        public float Amount;

        public UnityObjectRef<DamageType> Type;

        /// <summary>The <see cref="DamageType.garrisonFactor"/> of <see cref="Type"/>.</summary>
        public float GarrisonFactor;

        /// <summary>The <see cref="DamageType.coverPiercing"/> of <see cref="Type"/>.</summary>
        public float CoverPiercing;

        /// <summary>Suppression one hit adds to a squad, from 0 to 1.</summary>
        public float Suppression;

        /// <summary>Splash radius round the impact; 0 hits only the target.</summary>
        public float Radius;

        /// <summary>Fraction of <see cref="Amount"/> dealt at the splash edge, 1 = no falloff.</summary>
        public float EdgeFactor;

        /// <summary>Splash also hurts the source's allies.</summary>
        public bool FriendlyFire;

        /// <summary>The hit on <paramref name="target"/> at <paramref name="position"/>, its splash reaching <paramref name="reach"/>.</summary>
        public readonly DamageEvent Hit(Entity source, byte faction, float3 origin, Entity target, float3 position,
            WeaponTargets reach) => new()
        {
            Target = target,
            Position = position,
            Origin = origin,
            Source = source,
            SourceFaction = faction,
            Amount = Amount,
            Type = Type,
            GarrisonFactor = GarrisonFactor,
            CoverPiercing = CoverPiercing,
            Suppression = Suppression,
            Radius = Radius,
            EdgeFactor = EdgeFactor,
            FriendlyFire = FriendlyFire,
            Reach = reach,
        };
    }
}
