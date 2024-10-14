using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Core.Attack
{
    public class MeleeAttackDamageAuthoring : MonoBehaviour
    {
        public int Damage;

        public class Baker : Baker<MeleeAttackDamageAuthoring>
        {
            public override void Bake(MeleeAttackDamageAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new MeleeAttackDamage { Value = authoring.Damage });
            }
        }
    }
}
