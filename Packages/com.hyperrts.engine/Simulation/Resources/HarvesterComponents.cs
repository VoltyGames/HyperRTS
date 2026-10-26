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
    }

    /// <summary>Accepts harvested cargo from its owner's harvesters.</summary>
    public struct ResourceDropOff : IComponentData { }
}
