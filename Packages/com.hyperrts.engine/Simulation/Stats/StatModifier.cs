using System;
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

        public readonly StatModifier ToModifier(StatSource source) =>
            new() { Stat = stat, Add = add, Percent = percent, Source = source };
    }
}
