using Unity.Entities;
using Unity.Transforms;

namespace HyperRTS.Simulation.Vision
{
    /// <summary>Enemy root the local player stays drawn where it was last seen once it slips into the fog.</summary>
    public struct RememberedInFog : IComponentData { }

    /// <summary>What the local player last saw of a <see cref="RememberedInFog"/> root.</summary>
    public struct SeenState
    {
        public FogMemoryKey Key;
        public int TypeId;
        public byte Faction;
        public LocalTransform Transform;
        public float Health;
        public float Construction;
    }

    /// <summary>
    /// The latest <see cref="SeenState"/> of a visible enemy root. A cleanup component, so it outlives a ghost the
    /// server stops sending.
    /// </summary>
    public struct LatestSeen : ICleanupComponentData
    {
        public SeenState State;
    }

    /// <summary>A remembered building: drawn and shown on the minimap until the player sees its place again.</summary>
    public struct LastSeen : IComponentData
    {
        public SeenState State;

        /// <summary>Seconds the place has been in sight without the building turning up.</summary>
        public float VisibleFor;
    }
}
