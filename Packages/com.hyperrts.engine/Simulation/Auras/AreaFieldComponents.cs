using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Common;
using Unity.Entities;

namespace HyperRTS.Simulation.Auras
{
    /// <summary>An aura applied by <see cref="AreaFieldSystem"/>; its stat bonuses are in <see cref="AreaFieldBonus"/>.</summary>
    public struct AreaField : IComponentData
    {
        public int FieldId;
        public float Radius;
        public FieldTargets Affects;
        public float HealPerSecond;
        public float DamagePerSecond;
        public UnityObjectRef<DamageType> DamageType;
    }

    /// <summary>A modifier the field grants, with the field id as its source.</summary>
    [InternalBufferCapacity(1)]
    public struct AreaFieldBonus : IBufferElementData
    {
        public StatModifier Modifier;
    }

    /// <summary>A field this entity is inside, one per field id, sorted by id. Rewritten by <see cref="AreaFieldSystem"/>.</summary>
    [InternalBufferCapacity(0)]
    public struct FieldPresence : IBufferElementData
    {
        public int FieldId;

        /// <summary>The entity projecting the field (the lowest index when several overlap).</summary>
        public Entity Source;

        public byte SourceFaction;
    }
}
