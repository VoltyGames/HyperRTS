using HyperRTS.Core;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Units;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Simulation.Transport
{
    /// <summary>Lets a unit board transports and garrison buildings.</summary>
    [AddComponentMenu(HyperRTSMenu.Units + "Passenger")]
    [Icon(HyperRTSIcons.Units)]
    [HelpURL(HyperRTSDocs.Modules)]
    [DisallowMultipleComponent]
    [RequiresAuthoring(typeof(UnitAuthoring), "a Unit")]
    public class PassengerAuthoring : AuthoringBehaviour
    {
        [Tooltip("Space taken in a container (e.g. 1 for infantry, 3 for a light vehicle).")]
        [Min(1)]
        public int size = 1;

        public class Baker : Baker<PassengerAuthoring>
        {
            public override void Bake(PassengerAuthoring authoring)
            {
                var writer = new BakerWriter(this, GetEntity(TransformUsageFlags.Dynamic));
                TransportSetup.AddPassenger(ref writer, authoring.size);
            }
        }
    }
}
