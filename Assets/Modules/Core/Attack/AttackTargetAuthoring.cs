using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Core.Attack
{
    [AddComponentMenu(HyperRTSMenu.Attack + "Attack Target")]
    [Icon(HyperRTSIcons.Attack)]
    [HelpURL(HyperRTSDocs.WorldSetup)]
    [DisallowMultipleComponent]
    public class AttackTargetAuthoring : MonoBehaviour
    {
        [Tooltip("The entity this one attacks.")]
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
