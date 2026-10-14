using HyperRTS.Core;
using HyperRTS.Simulation.Common;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Simulation.Resources
{
    /// <summary>Tops up the owner's finite income reserves of one resource while this entity is owned and online.</summary>
    [AddComponentMenu(HyperRTSMenu.Resources + "Reserve Refill")]
    [Icon(HyperRTSIcons.Resources)]
    [HelpURL(HyperRTSDocs.Resources)]
    [DisallowMultipleComponent]
    public class ReserveRefillAuthoring : AuthoringBehaviour
    {
        [Tooltip("Resource whose income reserves are refilled.")]
        public ResourceType type;

        [Tooltip("Amount added to each reserve every interval, up to its capacity.")]
        [Min(1)]
        public int amount = 10;

        [Tooltip("Seconds between refills.")]
        [Min(0.1f)]
        public float interval = 5f;

        public class Baker : Baker<ReserveRefillAuthoring>
        {
            public override void Bake(ReserveRefillAuthoring authoring)
            {
                var writer = new BakerWriter(this, GetEntity(TransformUsageFlags.Dynamic));
                writer.Add(new ReserveRefill
                {
                    Type = authoring.type,
                    Amount = authoring.amount,
                    Interval = authoring.interval,
                });
            }
        }
    }
}
