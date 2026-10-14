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

        [Tooltip("Pay the amount once per passenger aboard (needs a Container); nothing while empty.")]
        public bool perOccupant;

        [Tooltip("Total it can pay out before running dry (a refill tops it up); 0 for endless.")]
        [Min(0)]
        public int reserve;

        public class Baker : Baker<ResourceIncomeAuthoring>
        {
            public override void Bake(ResourceIncomeAuthoring authoring)
            {
                var writer = new BakerWriter(this, GetEntity(TransformUsageFlags.Dynamic));
                writer.Add(new ResourceIncome
                {
                    Type = authoring.type,
                    Amount = authoring.amount,
                    Interval = authoring.interval,
                    FirstOwnerBonus = authoring.firstOwnerBonus,
                });
                if (authoring.perOccupant)
                {
                    writer.Add<OccupantIncome>();
                }

                if (authoring.reserve > 0)
                {
                    writer.Add(new ResourceReserve { Remaining = authoring.reserve, Capacity = authoring.reserve });
                }
            }
        }
    }
}
