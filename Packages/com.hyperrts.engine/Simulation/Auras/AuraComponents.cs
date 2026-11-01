using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Stats;
using Unity.Entities;

namespace HyperRTS.Simulation.Auras
{
    /// <summary>An aura applied by <see cref="AuraSystem"/>; its stat bonuses are in <see cref="AuraBonus"/>.</summary>
    public struct Aura : IComponentData
    {
        public int AuraId;
        public float Radius;
        public AuraTargets Affects;
        public float HealPerSecond;
        public float DamagePerSecond;
        public UnityObjectRef<DamageType> DamageType;
    }

    /// <summary>A modifier the aura grants, with the aura id as its source.</summary>
    [InternalBufferCapacity(1)]
    public struct AuraBonus : IBufferElementData
    {
        public StatModifier Modifier;
    }

    /// <summary>An aura this entity is inside, one per aura id, sorted by id. Rewritten by <see cref="AuraSystem"/>.</summary>
    [InternalBufferCapacity(0)]
    public struct AuraPresence : IBufferElementData
    {
        public int AuraId;

        /// <summary>The entity projecting the aura (the lowest index when several overlap).</summary>
        public Entity Source;

        public byte SourceFaction;
    }
}
