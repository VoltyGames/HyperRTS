using Unity.Entities;

namespace HyperRTS.Simulation.Resources
{
    /// <summary>
    /// While owned and not <c>Offline</c>, sends <see cref="Count"/> carriers every <see cref="Interval"/> from the map
    /// edge behind this entity; each pays the owner <see cref="Amount"/> when it arrives (supply convoys, cargo planes).
    /// </summary>
    public struct DeliverySource : IComponentData
    {
        public Entity Carrier;
        public int Count;
        public float Interval;
        public UnityObjectRef<ResourceType> Type;
        public int Amount;

        /// <summary>Distance from this entity's centre at which a carrier delivers; clear of its footprint.</summary>
        public float ArrivalRange;

        /// <summary>Sideways gap between carriers of one wave, so they don't spawn stacked.</summary>
        public float Spacing;

        public float Elapsed;
    }

    /// <summary>A carrier on its way to <see cref="Source"/>; pays its owner on arrival and nothing if killed.</summary>
    public struct DeliveryCarrier : IComponentData
    {
        public Entity Source;
        public UnityObjectRef<ResourceType> Type;
        public int Amount;
    }
}
