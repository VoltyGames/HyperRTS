using HyperRTS.Core;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Simulation.Attack
{
    [AddComponentMenu(HyperRTSMenu.Attack + "Melee Attack Damage")]
    [Icon(HyperRTSIcons.Attack)]
    [HelpURL(HyperRTSDocs.WorldSetup)]
    [DisallowMultipleComponent]
    public class MeleeAttackDamageAuthoring : MonoBehaviour
    {
        [Tooltip("Damage dealt per melee attack.")]
        public int damage = 10;

        public class Baker : Baker<MeleeAttackDamageAuthoring>
        {
            public override void Bake(MeleeAttackDamageAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new MeleeAttackDamage { Value = authoring.damage });
            }
        }
    }

    public struct MeleeAttackDamage : IComponentData
    {
        public int Value;
    }
}
