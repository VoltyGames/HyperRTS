using HyperRTS.Core;
using HyperRTS.Simulation.Buildings;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Units;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Simulation.Resources
{
    /// <summary>Carriers that bring resources to whoever owns this building, from the map edge behind it.</summary>
    [AddComponentMenu(HyperRTSMenu.Resources + "Delivery Source")]
    [Icon(HyperRTSIcons.Resources)]
    [HelpURL(HyperRTSDocs.Resources)]
    [DisallowMultipleComponent]
    [RequiresAuthoring(typeof(BuildingAuthoring), "a Building")]
    public class DeliverySourceAuthoring : AuthoringBehaviour
    {
        [Tooltip("Unit prefab that carries the delivery (a truck, a cargo plane); give it no weapons.")]
        public UnitAuthoring carrier;

        [Tooltip("Carriers sent per wave.")]
        [Min(1)]
        public int count = 1;

        [Tooltip("Seconds between waves.")]
        [Min(0.1f)]
        public float interval = 60f;

        [Tooltip("Resource each carrier pays on arrival.")]
        public ResourceType type;

        [Tooltip("Amount each carrier pays on arrival.")]
        [Min(1)]
        public int amount = 100;

        [Tooltip("Distance from this building's centre at which a carrier delivers; reach past the footprint.")]
        [Min(0.1f)]
        public float arrivalRange = 8f;

        [Tooltip("Sideways gap between carriers of one wave.")]
        [Min(0f)]
        public float spacing = 4f;

        public class Baker : Baker<DeliverySourceAuthoring>
        {
            public override void Bake(DeliverySourceAuthoring authoring)
            {
                // DeliverySourceRule reports a missing carrier or type.
                if (authoring.carrier == null || authoring.type == null)
                {
                    return;
                }

                var writer = new BakerWriter(this, GetEntity(TransformUsageFlags.Dynamic));
                DeliverySourceSetup.Add(ref writer, GetEntity(authoring.carrier, TransformUsageFlags.Dynamic),
                    authoring.count, authoring.interval, authoring.type, authoring.amount, authoring.arrivalRange,
                    authoring.spacing);
            }
        }
    }
}
