using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Core.Attack
{
    public struct AttackTarget : IComponentData
    {
        public Entity Value;
    }

    public class AttackTargetAuthoring : MonoBehaviour
    {
        public GameObject AttackTarget;

        public class Baker : Baker<AttackTargetAuthoring>
        {
            public override void Bake(AttackTargetAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity,
                    new AttackTarget { Value = GetEntity(authoring.AttackTarget, TransformUsageFlags.Dynamic) });
            }
        }
    }
}
