using Unity.Entities;
using Unity.Transforms;

namespace HyperRTS.Simulation.Vision
{
    /// <summary>Enemy root the local player keeps seeing where he last saw it once it slips into the fog.</summary>
    public struct FogMemory : IComponentData { }

    /// <summary>What the local player last saw of a <see cref="FogMemory"/> root.</summary>
    public struct FogRecord
    {
        public FogMemoryKey Key;
        public int TypeId;
        public byte Faction;
        public LocalTransform Transform;
        public float Health;
        public float Construction;
    }

    /// <summary>
    /// The latest <see cref="FogRecord"/> of a visible enemy root. A cleanup component, so it outlives a ghost the
    /// server stops sending.
    /// </summary>
    public struct FogSighting : ICleanupComponentData
    {
        public FogRecord Record;
    }

    /// <summary>A remembered building: drawn and shown on the minimap until the player sees its place again.</summary>
    public struct LastSeen : IComponentData
    {
        public FogRecord Record;

        /// <summary>Seconds the place has been in sight without the building turning up.</summary>
        public float VisibleFor;
    }
}
