using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Core.Attack
{
    /// <summary>
    /// Component for melee attack damage.
    /// </summary>
    public struct MeleeAttackDamage : IComponentData
    {
        public int Value;
    }

    public class MeleeAttackDamageAuthoring : MonoBehaviour
    {
        public int MeleeAttackDamage;

        public class Baker : Baker<MeleeAttackDamageAuthoring>
        {
            public override void Bake(MeleeAttackDamageAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new MeleeAttackDamage { Value = authoring.MeleeAttackDamage });
            }
        }
    }
}
