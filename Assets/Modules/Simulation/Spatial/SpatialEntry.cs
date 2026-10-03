using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Spatial
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
    }
}
