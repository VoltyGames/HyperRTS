using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Navigation
{
    /// <summary>Snapshot of one living, ownable entity, taken at the start of the movement phase.</summary>
    public struct SpatialEntry
    {
        public Entity Entity;
        public float3 Position;
        public float Radius;
        public byte Faction;

        /// <summary>False for buildings.</summary>
        public bool IsUnit;

        /// <summary>The <see cref="NavAgent"/> layer; Ground for entities without one.</summary>
        public NavLayer Layer;

        /// <summary>Half the <see cref="NavAgent"/> capsule's core segment on XZ; zero for round footprints.</summary>
        public float2 SegmentAxis;

        /// <summary>The <see cref="NavAgent.Spacing"/> it keeps; zero for entities without an agent.</summary>
        public float Spacing;

        /// <summary>Bounding radius of the footprint plus its spacing, for <see cref="SpatialIndex.QueryHulls{T}"/>.</summary>
        public readonly float HullReach => Radius + math.length(SegmentAxis) + Spacing;
    }
}
