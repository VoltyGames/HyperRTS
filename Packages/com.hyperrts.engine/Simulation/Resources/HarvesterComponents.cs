using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Resources
{
    /// <summary>Gathering stats and the cargo currently carried.</summary>
    public struct Harvester : IComponentData
    {
        public int Capacity;
        public float GatherRate;

        /// <summary>How far from a depleted node it looks for another of the same type.</summary>
        public float ReplacementRange;

        public UnityObjectRef<ResourceType> CargoType;
        public int CargoAmount;
    }

    /// <summary>Leg of the gather loop a harvester is on.</summary>
    public enum HarvestPhase : byte
    {
        Gathering = 0,
        Returning = 1,
    }

    /// <summary>Gather-loop bookkeeping for a <see cref="Harvester"/> on a Gather order.</summary>
    public struct HarvestState : IComponentData
    {
        public HarvestPhase Phase;

        /// <summary>Last node gathered from, so a replacement of the same type can be found near it.</summary>
        public float3 NodePosition;

        /// <summary>Fractional gathering carried between frames.</summary>
        public float Progress;

        /// <summary>Where this trip delivers, chosen once so a turning truck doesn't flip between two.</summary>
        public Entity DropOff;

        /// <summary>The dock whose bay it holds or waits in line for.</summary>
        public Entity Dock;

        public int Bay;

        /// <summary>Its place in the bay's line, 1 at the front; 0 while it holds the bay.</summary>
        public int Place;

        /// <summary>Seconds until a vehicle in line tries again for a bay.</summary>
        public float RetryIn;
    }

    /// <summary>Accepts harvested cargo from its owner's harvesters.</summary>
    public struct ResourceDropOff : IComponentData { }
}
