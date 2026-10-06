using HyperRTS.Core;
using HyperRTS.Simulation.Common;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Simulation.Transport
{
    /// <summary>Lets allied passengers board this unit (transport) or garrison this building (bunker).</summary>
    [AddComponentMenu(HyperRTSMenu.Units + "Container")]
    [Icon(HyperRTSIcons.Units)]
    [HelpURL(HyperRTSDocs.Modules)]
    [DisallowMultipleComponent]
    [RequiresAuthoring(typeof(GameEntityAuthoring), "a Unit or Building")]
    public class ContainerAuthoring : MonoBehaviour
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

        public class Baker : Baker<ContainerAuthoring>
        {
            public override void Bake(ContainerAuthoring authoring)
            {
                var sink = new BakerSink(this, GetEntity(TransformUsageFlags.Dynamic));
                TransportSetup.AddContainer(ref sink, new Container
                {
                    Capacity = authoring.capacity,
                    MaxPassengerSize = authoring.maxPassengerSize,
                    PassengersFire = authoring.passengersFire,
                    PassengersSurvive = authoring.passengersSurvive,
                });
            }
        }
    }

    /// <summary>Holds passengers listed in its <see cref="Cargo"/> buffer.</summary>
    public struct Container : IComponentData
    {
        public int Capacity;
        public int MaxPassengerSize;
        public bool PassengersFire;
        public bool PassengersSurvive;

        public readonly bool Fits(DynamicBuffer<Cargo> cargo, int size)
        {
            var used = 0;
            foreach (var item in cargo)
            {
                used += item.Size;
            }

            return size <= MaxPassengerSize && used + size <= Capacity;
        }
    }
}
