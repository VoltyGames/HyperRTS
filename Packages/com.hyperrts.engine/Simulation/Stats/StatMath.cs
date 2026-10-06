using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Stats
{
    /// <summary>Modifier arithmetic and bookkeeping. Buffers are tiny, so linear scans are fine.</summary>
    public static class StatMath
    {
        public static float Evaluate(DynamicBuffer<StatModifier> modifiers, Stat stat, float baseValue)
        {
            Totals(modifiers, stat, out var add, out var percent);
            return math.max(0f, (baseValue + add) * (1f + percent));
        }

        /// <summary>The summed <see cref="StatModifier.Add"/> and <see cref="StatModifier.Percent"/> of a stat.</summary>
        public static void Totals(DynamicBuffer<StatModifier> modifiers, Stat stat, out float add, out float percent)
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

        /// <summary>The modified value of a stat currently at <paramref name="live"/>; see <see cref="BaseOf"/>.</summary>
        public static float Apply(DynamicBuffer<StatModifier> modifiers, DynamicBuffer<BaseStat> bases, Stat stat,
            float live) =>
            Evaluate(modifiers, stat, BaseOf(bases, stat, live));

        /// <summary>
        /// The stat's base value, captured from <paramref name="live"/> on first use: afterwards the live value holds
        /// modified results, and only the base still remembers the authored one.
        /// </summary>
        public static float BaseOf(DynamicBuffer<BaseStat> bases, Stat stat, float live)
        {
            foreach (var item in bases)
            {
                if (item.Stat == stat)
                {
                    return item.Value;
                }
            }

            bases.Add(new BaseStat { Stat = stat, Value = live });
            return live;
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
    }
}
