using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Core.Attack
{
    public class RangeAttackDamageAuthoring : MonoBehaviour
    {
        public int Damage;

        public class Baker : Baker<RangeAttackDamageAuthoring>
        {
            public override void Bake(RangeAttackDamageAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new RangeAttackDamage { Value = authoring.Damage });
            }
        }
    }
}
