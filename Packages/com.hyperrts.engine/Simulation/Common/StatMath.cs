using System;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Common
{
    /// <summary>Modifier arithmetic and bookkeeping. Buffers are tiny, so linear scans are fine.</summary>
    public static class StatMath
    {
        public static float Evaluate(DynamicBuffer<StatModifier> modifiers, Stat stat, float baseValue)
        {
            SumModifiers(modifiers, stat, out var add, out var percent);
            return math.max(0f, (baseValue + add) * (1f + percent));
        }

        /// <summary>The summed <see cref="StatModifier.Add"/> and <see cref="StatModifier.Percent"/> of a stat.</summary>
        public static void SumModifiers(DynamicBuffer<StatModifier> modifiers, Stat stat, out float add,
            out float percent)
        {
            add = 0f;
            percent = 0f;
            foreach (var modifier in modifiers)
            {
                if (modifier.Stat == stat)
                {
                    add += modifier.Add;
                    percent += modifier.Percent;
                }
            }
        }

        /// <summary>The modified value of a stat from its recorded base.</summary>
        public static float Apply(DynamicBuffer<StatModifier> modifiers, DynamicBuffer<BaseStat> bases, Stat stat) =>
            Evaluate(modifiers, stat, BaseValue(bases, stat));

        /// <summary>The stat's base value, recorded when the entity spawned.</summary>
        public static float BaseValue(DynamicBuffer<BaseStat> bases, Stat stat)
        {
            foreach (var item in bases)
            {
                if (item.Stat == stat)
                {
                    return item.Value;
                }
            }

            throw new InvalidOperationException("The entity has no recorded base for this stat.");
        }

        /// <summary>Incoming damage multiplier; 1 for entities without modifiers.</summary>
        public static float DamageTaken(in BufferLookup<StatModifier> modifiers, Entity entity) =>
            modifiers.TryGetBuffer(entity, out var buffer) ? Evaluate(buffer, Stat.DamageTaken, 1f) : 1f;

        /// <summary>Removes every modifier from <paramref name="source"/>.</summary>
        public static void RemoveSource(DynamicBuffer<StatModifier> modifiers, StatSource source)
        {
            for (var i = modifiers.Length - 1; i >= 0; i--)
            {
                if (modifiers[i].Source.Equals(source))
                {
                    modifiers.RemoveAtSwapBack(i);
                }
            }
        }

        /// <summary>Removes every modifier whose source is of <paramref name="kind"/>.</summary>
        public static void RemoveKind(DynamicBuffer<StatModifier> modifiers, StatSourceKind kind)
        {
            for (var i = modifiers.Length - 1; i >= 0; i--)
            {
                if (modifiers[i].Source.Kind == kind)
                {
                    modifiers.RemoveAtSwapBack(i);
                }
            }
        }
    }
}
