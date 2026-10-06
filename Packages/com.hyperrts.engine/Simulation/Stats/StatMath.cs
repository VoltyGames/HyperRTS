using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Stats
{
    /// <summary>Modifier arithmetic and bookkeeping. Buffers are tiny, so linear scans are fine.</summary>
    public static class StatMath
    {
        /// <summary>Source id for veterancy bonuses; upgrades and fields use their type ids.</summary>
        public const int VeterancySource = 1;

        public static float Evaluate(DynamicBuffer<StatModifier> modifiers, Stat stat, float baseValue)
        {
            var add = 0f;
            var percent = 0f;
            foreach (var modifier in modifiers)
            {
                if (modifier.Stat == stat)
                {
                    add += modifier.Add;
                    percent += modifier.Percent;
                }
            }

            return math.max(0f, (baseValue + add) * (1f + percent));
        }

        /// <summary>Incoming damage multiplier; 1 for entities without modifiers.</summary>
        public static float DamageTaken(in BufferLookup<StatModifier> modifiers, Entity entity) =>
            modifiers.TryGetBuffer(entity, out var buffer) ? Evaluate(buffer, Stat.DamageTaken, 1f) : 1f;

        /// <summary>Removes every modifier from <paramref name="source"/>.</summary>
        public static void RemoveSource(DynamicBuffer<StatModifier> modifiers, int source)
        {
            for (var i = modifiers.Length - 1; i >= 0; i--)
            {
                if (modifiers[i].Source == source)
                {
                    modifiers.RemoveAtSwapBack(i);
                }
            }
        }
    }
}
