using HyperRTS.Core;
using HyperRTS.Simulation.Buildings;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Cues;
using HyperRTS.Simulation.Resources;
using HyperRTS.Simulation.Units;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Simulation.Airdrops
{
    /// <summary>A structure that an aircraft from its owner's map edge drops a crate of resources on, every interval.</summary>
    [AddComponentMenu(HyperRTSMenu.Resources + "Airdrop Receiver")]
    [Icon(HyperRTSIcons.Resources)]
    [HelpURL(HyperRTSDocs.Airdrops)]
    [DisallowMultipleComponent]
    [RequiresAuthoring(typeof(BuildingAuthoring), "a Building")]
    public class AirdropReceiverAuthoring : AuthoringBehaviour
    {
        [Tooltip("Unarmed aircraft prefab with Flight that flies the delivery; enemy air defence can shoot it down.")]
        public UnitAuthoring aircraft;

        [Tooltip("Ghost prefab of the cargo under its parachutes, sinking from the aircraft onto this structure.")]
        public GameObject crate;

        [Tooltip("Optional point on this structure the crate lands on, such as the middle of a drop field; empty lands it on the origin.")]
        public Transform landingPoint;

        [Tooltip("Resource the crate pays when it lands.")]
        public ResourceType type;

        [Tooltip("Amount the crate pays when it lands.")]
        [Min(1)]
        public int amount = 180;

        [Tooltip("Seconds between deliveries while finished and powered.")]
        [Min(1f)]
        public float interval = 90f;

        [Tooltip("Cruise height of the aircraft above this structure.")]
        [Min(1f)]
        public float altitude = 30f;

        [Tooltip("Metres per second the crate sinks under its parachutes.")]
        [Min(0.1f)]
        public float fallSpeed = 6f;

        public class Baker : Baker<AirdropReceiverAuthoring>
        {
            public override void Bake(AirdropReceiverAuthoring authoring)
            {
                // AirdropReceiverRule reports a missing aircraft, crate or type.
                if (authoring.aircraft == null || authoring.crate == null || authoring.type == null)
                {
                    return;
                }

                var writer = new BakerWriter(this, GetEntity(TransformUsageFlags.Dynamic));
                if (authoring.landingPoint != null)
                {
                    DependsOn(authoring.landingPoint);
                }

                writer.Add(new AirdropReceiver
                {
                    Aircraft = GetEntity(authoring.aircraft, TransformUsageFlags.Dynamic),
                    Crate = GetEntity(authoring.crate, TransformUsageFlags.Dynamic),
                    Type = authoring.type,
                    Amount = authoring.amount,
                    Interval = authoring.interval,
                    Altitude = authoring.altitude,
                    FallSpeed = authoring.fallSpeed,
                    Landing = EffectSetup.Socket(authoring.transform, authoring.landingPoint),
                });
            }
        }
    }
}
