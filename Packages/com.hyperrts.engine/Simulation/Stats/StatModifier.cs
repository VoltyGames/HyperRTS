using System;
using System.Collections.Generic;
using HyperRTS.Simulation.Common.Combat;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Simulation.Stats
{
    /// <summary>
    /// A change to one stat: <c>(base + Add) × (1 + sum of Percent)</c>. <see cref="Source"/> identifies who added it
    /// (an upgrade, veterancy, a field) so it can be removed again.
    /// </summary>
    [InternalBufferCapacity(2)]
    public struct StatModifier : IBufferElementData
    {
        public Stat Stat;
        public float Add;
        public float Percent;

        /// <summary>Limits a <see cref="Stats.Stat.DamageTaken"/> modifier to hits of this type; unset for every hit.</summary>
        public UnityObjectRef<DamageType> DamageType;

        public StatSource Source;
    }

    /// <summary>Inspector form of a <see cref="StatModifier"/>; the owner supplies the source.</summary>
    [Serializable]
    public struct StatBonus
    {
        [Tooltip("Stat to change.")]
        public Stat stat;

        [Tooltip("Flat amount added to the base value.")]
        public float add;

        [Tooltip("Fraction added on top: 0.25 = +25%, -0.5 = half. Percents from every source add up.")]
        public float percent;

        [Tooltip("Damage Taken only: limits the bonus to hits of this damage type. Leave empty for every hit.")]
        public DamageType damageType;

        /// <summary>One modifier per bonus, all from <paramref name="source"/>.</summary>
        public static List<StatModifier> ToModifiers(IReadOnlyList<StatBonus> bonuses, StatSource source)
        {
            var modifiers = new List<StatModifier>(bonuses.Count);
            foreach (var bonus in bonuses)
            {
                if (bonus.damageType != null && bonus.stat != Stat.DamageTaken)
                {
                    throw new InvalidOperationException(
                        $"A {bonus.stat} bonus names damage type '{bonus.damageType.name}'; only Damage Taken filters by type.");
                }

                modifiers.Add(new StatModifier
                {
                    Stat = bonus.stat, Add = bonus.add, Percent = bonus.percent, DamageType = bonus.damageType,
                    Source = source,
                });
            }

            return modifiers;
        }
    }
}
