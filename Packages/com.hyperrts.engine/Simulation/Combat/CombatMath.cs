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

        /// <summary>Subtracts armor-scaled damage; ignores targets that are already gone or dead.</summary>
        public static void ApplyDamage(ref ComponentLookup<Health> health, in BufferLookup<ArmorModifier> armor,
            Entity target, float damage, UnityObjectRef<DamageType> damageType)
        {
            if (!health.TryGetComponent(target, out var current) || current.Current <= 0f)
            {
                return;
            }

            current.Current = math.max(0f, current.Current - damage * ArmorMultiplier(armor, target, damageType));
            health[target] = current;
        }
    }
}
