using HyperRTS.Core;
using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Common;
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

        [Tooltip("A neutral building anyone may garrison; it belongs to whoever is inside, and is neutral again when empty.")]
        public bool neutralGarrison;

        [Tooltip("Child transform passengers step out at, such as a door; none lets them out around the container.")]
        public Transform exit;

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
                    Exit = authoring.exit != null ? WeaponAuthoring.WeaponStats.Pivot(this, authoring.transform, authoring.exit) : default,
                });
                if (authoring.neutralGarrison)
                {
                    writer.Add<NeutralGarrison>();
                }
            }
        }
    }
}
