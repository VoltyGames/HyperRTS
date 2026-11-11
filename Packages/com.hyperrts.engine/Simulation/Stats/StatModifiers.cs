using HyperRTS.Simulation.Common.Combat;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Stats
{
    /// <summary>An entity's <see cref="StatModifier"/> buffer with the arithmetic and removal over it. Buffers are tiny, so linear scans are fine.</summary>
    public struct StatModifiers
    {
        private DynamicBuffer<StatModifier> _modifiers;

        public StatModifiers(DynamicBuffer<StatModifier> modifiers) => _modifiers = modifiers;

        /// <summary>The stat's value from <paramref name="baseValue"/> and its modifiers without a damage type.</summary>
        public readonly float Evaluate(Stat stat, float baseValue) => Evaluate(stat, default, baseValue);

        /// <summary>The stat's value counting only the modifiers filtered to <paramref name="damageType"/>.</summary>
        public readonly float Evaluate(Stat stat, UnityObjectRef<DamageType> damageType, float baseValue)
        {
            Sum(stat, damageType, out var add, out var percent);
            return math.max(0f, (baseValue + add) * (1f + percent));
        }

        /// <summary>The summed <see cref="StatModifier.Add"/> and <see cref="StatModifier.Percent"/> of a stat.</summary>
        public readonly void Sum(Stat stat, UnityObjectRef<DamageType> damageType, out float add, out float percent)
        {
            add = 0f;
            percent = 0f;
            foreach (var modifier in _modifiers)
            {
                if (modifier.Stat == stat && modifier.DamageType.Equals(damageType))
                {
                    add += modifier.Add;
                    percent += modifier.Percent;
                }
            }
        }

        /// <summary>The modified value of a stat from its recorded base.</summary>
        public readonly float Apply(DynamicBuffer<BaseStat> bases, Stat stat) =>
            Evaluate(stat, BaseStat.Read(bases, stat));

        /// <summary>
        /// Incoming damage multiplier for a hit of <paramref name="damageType"/>: the untyped modifiers times those of
        /// that type, each floored at 0 so a large resist never heals. 1 for entities without modifiers.
        /// </summary>
        public static float DamageTaken(in BufferLookup<StatModifier> lookup, Entity entity,
            UnityObjectRef<DamageType> damageType)
        {
            if (!lookup.TryGetBuffer(entity, out var buffer))
            {
                return 1f;
            }

            var modifiers = new StatModifiers(buffer);
            var untyped = modifiers.Evaluate(Stat.DamageTaken, 1f);
            var plain = damageType.Equals(default(UnityObjectRef<DamageType>));
            return plain ? untyped : untyped * modifiers.Evaluate(Stat.DamageTaken, damageType, 1f);
        }

        /// <summary>Removes every modifier from <paramref name="source"/>.</summary>
        public void RemoveSource(StatSource source)
        {
            for (var i = _modifiers.Length - 1; i >= 0; i--)
            {
                if (_modifiers[i].Source.Equals(source))
                {
                    _modifiers.RemoveAtSwapBack(i);
                }
            }
        }

        /// <summary>Removes every modifier whose source is of <paramref name="kind"/>.</summary>
        public void RemoveKind(StatSourceKind kind)
        {
            for (var i = _modifiers.Length - 1; i >= 0; i--)
            {
                if (_modifiers[i].Source.Kind == kind)
                {
                    _modifiers.RemoveAtSwapBack(i);
                }
            }
        }
    }
}
