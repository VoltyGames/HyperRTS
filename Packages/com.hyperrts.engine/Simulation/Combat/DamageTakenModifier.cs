using System;
using HyperRTS.Simulation.Common;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace HyperRTS.Simulation.Combat
{
    /// <summary>
    /// Extra damage taken from one damage type, on top of <see cref="Stat.DamageTaken"/>. Percents add up, and
    /// <see cref="Source"/> lets the owner remove its own.
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct DamageTakenModifier : IBufferElementData
    {
        public UnityObjectRef<DamageType> DamageType;
        public float Percent;
        public StatSource Source;

        /// <summary>1 plus every percent matching <paramref name="type"/>, floored at 0 so a large resist never heals.</summary>
        public static float Multiplier(DynamicBuffer<DamageTakenModifier> modifiers, UnityObjectRef<DamageType> type)
        {
            var percent = 0f;
            foreach (var modifier in modifiers)
            {
                if (modifier.DamageType.Equals(type))
                {
                    percent += modifier.Percent;
                }
            }

            return math.max(0f, 1f + percent);
        }

        public static void RemoveSource(DynamicBuffer<DamageTakenModifier> modifiers, StatSource source)
        {
            for (var i = modifiers.Length - 1; i >= 0; i--)
            {
                if (modifiers[i].Source.Equals(source))
                {
                    modifiers.RemoveAtSwapBack(i);
                }
            }
        }
    }

    /// <summary>Inspector form of a <see cref="DamageTakenModifier"/>; the owner supplies the source.</summary>
    [Serializable]
    public struct TypedDamageBonus
    {
        [Tooltip("Damage type the bonus applies to.")]
        public DamageType damageType;

        [Tooltip("Extra damage taken from that type: 0.3 = +30%, -0.5 = half.")]
        public float percent;

        public readonly DamageTakenModifier ToModifier(StatSource source) =>
            new() { DamageType = damageType, Percent = percent, Source = source };
    }
}
