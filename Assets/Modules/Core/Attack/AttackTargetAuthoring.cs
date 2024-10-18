using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Core.Attack
{
    public class AttackTargetAuthoring : MonoBehaviour
    {
        public GameObject target;

        public class Baker : Baker<AttackTargetAuthoring>
        {
            public override void Bake(AttackTargetAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity,
                    new AttackTarget { Value = GetEntity(authoring.target, TransformUsageFlags.Dynamic) });
            }
        }
    }
    
    public struct AttackTarget : IComponentData
    {
        public Entity Value;
    }
}
