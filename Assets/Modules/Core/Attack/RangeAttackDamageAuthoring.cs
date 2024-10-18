using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Core.Attack
{
    public class RangeAttackDamageAuthoring : MonoBehaviour
    {
        public int damage;

        public class Baker : Baker<RangeAttackDamageAuthoring>
        {
            public override void Bake(RangeAttackDamageAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new RangeAttackDamage { Value = authoring.damage });
            }
        }
    }
    
    /// <summary>
    /// Component for melee attack damage.
    /// </summary>
    public struct RangeAttackDamage : IComponentData
    {
        public int Value;
    }
}
