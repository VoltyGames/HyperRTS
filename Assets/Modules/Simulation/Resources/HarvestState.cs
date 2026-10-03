using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Resources
{
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

        public UnityObjectRef<ResourceType> NodeType;

        /// <summary>Fractional gathering carried between frames.</summary>
        public float Progress;
    }
}
