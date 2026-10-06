using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Combat
{
    /// <summary>Range and damage rules shared by the combat systems.</summary>
    public static class CombatMath
    {
        /// <summary>Gap between two footprints on the XZ plane; weapon ranges are measured edge to edge.</summary>
        public static float EdgeDistance(float3 a, float radiusA, float3 b, float radiusB) =>
            math.max(0f, math.distance(a.xz, b.xz) - radiusA - radiusB);

        public static float AcquireRange(in Weapon weapon, float visionRange) =>
            math.max(weapon.AcquireRange > 0f ? weapon.AcquireRange : visionRange, weapon.Range);

        public static float ArmorMultiplier(in BufferLookup<ArmorModifier> armor, Entity target,
            UnityObjectRef<DamageType> damageType)
        {
            if (!armor.TryGetBuffer(target, out var modifiers))
            {
                return 1f;
            }

            foreach (var modifier in modifiers)
            {
                if (modifier.DamageType.Equals(damageType))
                {
                    return modifier.Multiplier;
                }
            }

            return 1f;
        }

        /// <summary>Linear falloff from full damage at the centre to <paramref name="edgeFactor"/> at the radius.</summary>
        public static float SplashFactor(float gap, float radius, float edgeFactor) =>
            math.lerp(1f, edgeFactor, math.saturate(gap / math.max(radius, 1e-4f)));

        /// <summary>The hit a weapon deals: full damage to the target plus its splash, if any.</summary>
        public static DamageEvent Hit(in Weapon weapon, Entity source, byte faction, float3 origin, Entity target,
            float3 position) => new()
        {
            Target = target,
            Position = position,
            Origin = origin,
            Source = source,
            SourceFaction = faction,
            Amount = weapon.Damage,
            Type = weapon.DamageType,
            Radius = weapon.SplashRadius,
            EdgeFactor = weapon.SplashEdgeFactor,
            FriendlyFire = weapon.FriendlyFire,
        };
    }
}
