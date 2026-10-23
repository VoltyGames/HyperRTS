using HyperRTS.Simulation.Resources;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Airdrops
{
    /// <summary>
    /// While owned and not <c>Offline</c>, sends <see cref="Aircraft"/> every <see cref="Interval"/> from the owner's
    /// map edge to drop a <see cref="Crate"/> worth <see cref="Amount"/> on this structure.
    /// </summary>
    public struct AirdropReceiver : IComponentData
    {
        public Entity Aircraft;
        public Entity Crate;
        public UnityObjectRef<ResourceType> Type;
        public int Amount;
        public float Interval;

        /// <summary>Cruise height above the structure.</summary>
        public float Altitude;

        /// <summary>Metres per second the crate sinks under its parachutes.</summary>
        public float FallSpeed;

        /// <summary>Where the crate lands, in the structure's local space.</summary>
        public float3 Landing;

        public float Elapsed;
    }

    /// <summary>An aircraft flying a delivery to <see cref="Receiver"/>; shot down before the drop, the delivery is lost.</summary>
    public struct AirdropFlight : IComponentData
    {
        public Entity Receiver;
        public DropRoute Route;

        /// <summary>Set once it dropped or its receiver was lost; it then flies off the map and despawns.</summary>
        public bool Leaving;
    }

    /// <summary>Cargo sinking onto <see cref="Receiver"/>; pays the receiver's owner when it lands.</summary>
    public struct AirdropCrate : IComponentData
    {
        public Entity Receiver;
        public UnityObjectRef<ResourceType> Type;
        public int Amount;
        public float FallSpeed;
        public float Ground;
    }
}
