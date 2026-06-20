using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Core.Attack
{
    [AddComponentMenu(HyperRTSMenu.Attack + "Ranged Attack Damage")]
    [Icon(HyperRTSIcons.Attack)]
    [HelpURL(HyperRTSDocs.WorldSetup)]
    [DisallowMultipleComponent]
    public class RangeAttackDamageAuthoring : MonoBehaviour
    {
        [Tooltip("Damage dealt per ranged attack.")]
        public int damage = 10;

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
    /// Component for ranged attack damage.
    /// </summary>
    public struct RangeAttackDamage : IComponentData
    {
        public int Value;
    }
}
