using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Common;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Status
{
    /// <summary>What applying an effect that is already active does.</summary>
    public enum StatusStackRule : byte
    {
        /// <summary>Restarts the duration; stacks stay at one.</summary>
        Refresh = 0,

        /// <summary>Restarts the duration and adds a stack, up to the maximum.</summary>
        AddStack = 1,
    }

    /// <summary>
    /// A status effect prefab's rules; its type id is the prefab's <c>EntityInfo.TypeId</c>. Bonuses are in
    /// <see cref="StatusStatBonus"/> and <see cref="StatusDamageBonus"/>, and scale with stacks.
    /// </summary>
    public struct StatusEffectDefinition : IComponentData
    {
        public float Duration;
        public int MaxStacks;
        public StatusStackRule Rule;

        /// <summary>Damage each second per stack, credited to the effect's source; 0 for none.</summary>
        public float DamagePerSecond;

        public UnityObjectRef<DamageType> DamageType;

        /// <summary>The stack count after applying the effect again on top of <paramref name="stacks"/>.</summary>
        public readonly int NextStacks(int stacks) =>
            Rule == StatusStackRule.AddStack ? math.min(MaxStacks, stacks + 1) : 1;
    }

    /// <summary>A stat modifier one stack grants, with the effect as its source.</summary>
    [InternalBufferCapacity(1)]
    public struct StatusStatBonus : IBufferElementData
    {
        public StatModifier Modifier;
    }

    /// <summary>A damage-type-filtered damage-taken modifier one stack grants, with the effect as its source.</summary>
    [InternalBufferCapacity(1)]
    public struct StatusDamageBonus : IBufferElementData
    {
        public DamageTakenModifier Modifier;
    }
}
