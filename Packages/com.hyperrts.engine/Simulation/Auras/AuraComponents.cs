using HyperRTS.Simulation.Combat.Damage;
using HyperRTS.Simulation.Stats;
using Unity.Entities;

namespace HyperRTS.Simulation.Auras
{
    /// <summary>An aura applied by <see cref="AuraSystem"/>; its stat bonuses are its <see cref="GrantedModifier"/>s.</summary>
    public struct Aura : IComponentData
    {
        public int AuraId;
        public float Radius;
        public AuraTargets Affects;
        public float HealPerSecond;
        public float DamagePerSecond;
        public DamageProfile Damage;
    }

    /// <summary>An aura this entity is inside, one per aura id, sorted by id. Rewritten by <see cref="AuraSystem"/>.</summary>
    [InternalBufferCapacity(0)]
    public struct AuraPresence : IBufferElementData
    {
        public int AuraId;

        /// <summary>The entity projecting the aura (the lowest index when several overlap).</summary>
        public Entity Source;

        public byte SourceOwner;
    }
}
