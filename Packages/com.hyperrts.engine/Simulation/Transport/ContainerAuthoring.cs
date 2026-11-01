using HyperRTS.Core;
using HyperRTS.Simulation.Combat.Weapons;
using HyperRTS.Simulation.Common.Authoring;
using HyperRTS.Simulation.GameEntities;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Simulation.Transport
{
    /// <summary>Lets allied passengers board this unit (transport) or garrison this building (bunker).</summary>
    [AddComponentMenu(HyperRTSMenu.Units + "Container")]
    [Icon(HyperRTSIcons.Units)]
    [HelpURL(HyperRTSDocs.Transport)]
    [DisallowMultipleComponent]
    [RequiresAuthoring(typeof(GameEntityAuthoring), "a Unit or Building")]
    public class ContainerAuthoring : AuthoringBehaviour
    {
        [Tooltip("Total passenger size that fits.")]
        [Min(1)]
        public int capacity = 5;

        [Tooltip("Largest single passenger size allowed (1 = infantry only, if infantry are size 1).")]
        [Min(1)]
        public int maxPassengerSize = 1;

        [Tooltip("Passengers' weapons fire from inside (garrisons, battle buses).")]
        public bool passengersFire;

        [Tooltip("Passengers get out when this is destroyed; otherwise they die with it.")]
        public bool passengersSurvive = true;

        [Tooltip("Share of each hit on this the walls stop before it reaches the passengers, at full health; it falls with health. 1 keeps them out of harm.")]
        [Range(0f, 1f)]
        public float protection = 1f;

        [Tooltip("A neutral building anyone may garrison; it belongs to whoever is inside, and is neutral again when empty.")]
        public bool neutralGarrison;

        [Tooltip("Child transform passengers step out at, such as a door; none lets them out around the container.")]
        public Transform exit;

        [Tooltip("Child transforms at windows and loopholes that passengers fire out of, their blue axis pointing outward; none fires from the centre.")]
        public Transform[] firingPorts;

        /// <summary>The port at <paramref name="port"/> in this container's local space.</summary>
        private FiringPort LocalPort(Transform port) => new()
        {
            Position = transform.InverseTransformPoint(port.position),
            Direction = transform.InverseTransformDirection(port.forward),
        };

        public class Baker : Baker<ContainerAuthoring>
        {
            public override void Bake(ContainerAuthoring authoring)
            {
                var writer = new BakerWriter(this, GetEntity(TransformUsageFlags.Dynamic));
                TransportSetup.AddContainer(ref writer, new Container
                {
                    Capacity = authoring.capacity,
                    MaxPassengerSize = authoring.maxPassengerSize,
                    PassengersFire = authoring.passengersFire,
                    PassengersSurvive = authoring.passengersSurvive,
                    HasExit = authoring.exit != null,
                    Exit = AuthoringBehaviour.Socket(this, authoring.transform, authoring.exit),
                });
                if (authoring.firingPorts is { Length: > 0 })
                {
                    var ports = writer.AddBuffer<FiringPort>();
                    foreach (var port in authoring.firingPorts)
                    {
                        DependsOn(port);
                        ports.Add(authoring.LocalPort(port));
                    }
                }

                if (authoring.protection < 1f)
                {
                    TransportSetup.AddShelter(ref writer, authoring.protection);
                }

                if (authoring.neutralGarrison)
                {
                    writer.Add<NeutralGarrison>();
                }
            }
        }
    }
}
