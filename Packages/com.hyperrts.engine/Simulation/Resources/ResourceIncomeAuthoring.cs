using HyperRTS.Core;
using HyperRTS.Simulation.Common;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Simulation.Resources
{
    /// <summary>Passive income for whoever owns this entity (capturable oil derricks, supply outposts).</summary>
    [AddComponentMenu(HyperRTSMenu.Resources + "Resource Income")]
    [Icon(HyperRTSIcons.Resources)]
    [HelpURL(HyperRTSDocs.Resources)]
    [DisallowMultipleComponent]
    public class ResourceIncomeAuthoring : AuthoringBehaviour
    {
        [Tooltip("Resource paid to the owner.")]
        public ResourceType type;

        [Tooltip("Amount paid every interval.")]
        [Min(1)]
        public int amount = 10;

        [Tooltip("Seconds between payments.")]
        [Min(0.1f)]
        public float interval = 5f;

        [Tooltip("Paid once to the first player to own it; 0 for none.")]
        [Min(0)]
        public int firstOwnerBonus;

        public class Baker : Baker<ResourceIncomeAuthoring>
        {
            public override void Bake(ResourceIncomeAuthoring authoring)
            {
                var writer = new BakerWriter(this, GetEntity(TransformUsageFlags.Dynamic));
                ResourceIncomeSetup.Add(ref writer, authoring.type, authoring.amount, authoring.interval,
                    authoring.firstOwnerBonus);
            }
        }
    }
}
