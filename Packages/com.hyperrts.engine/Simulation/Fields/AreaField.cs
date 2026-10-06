using HyperRTS.Simulation.Combat;
using Unity.Entities;

namespace HyperRTS.Simulation.Fields
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
}
