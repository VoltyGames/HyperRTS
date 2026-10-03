using System;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Spatial
{
    [Flags]
    public enum SpatialFlags : byte
    {
        None = 0,
        Unit = 1,
        Building = 2,
    }

    /// <summary>Snapshot of one living, ownable entity, taken at the start of the movement phase.</summary>
    public struct SpatialEntry
    {
        public Entity Entity;
        public float3 Position;
        public float Radius;
        public byte Faction;
        public SpatialFlags Flags;
    }

    /// <summary>Callback for <see cref="SpatialIndex.Query{T}"/>; a struct so Burst can inline it.</summary>
    public interface ISpatialVisitor
    {
        void Visit(in SpatialEntry entry);
    }
}
