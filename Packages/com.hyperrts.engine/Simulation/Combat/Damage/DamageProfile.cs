using HyperRTS.Simulation.Combat.Weapons;
using HyperRTS.Simulation.Common.Combat;
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

        /// <summary>A profile that only carries <paramref name="type"/> and its cover piercing, for damage over time.</summary>
        public static DamageProfile FromType(DamageType type) => new()
        {
            Type = type,
            CoverPiercing = DamageType.Piercing(type),
        };

        /// <summary>Linear falloff from full damage at the centre to <see cref="EdgeFactor"/> at <see cref="Radius"/>.</summary>
        public readonly float SplashFactor(float gap) =>
            math.lerp(1f, EdgeFactor, math.saturate(gap / math.max(Radius, 1e-4f)));

        /// <summary>The hit on <paramref name="target"/> at <paramref name="position"/>, its splash reaching <paramref name="layers"/>.</summary>
        public readonly DamageEvent Hit(Entity source, byte owner, float3 origin, Entity target, float3 position,
            WeaponTargets layers) => new()
        {
            Target = target,
            Position = position,
            Origin = origin,
            Source = source,
            SourceOwner = owner,
            Profile = this,
            Layers = layers,
        };
    }
}
