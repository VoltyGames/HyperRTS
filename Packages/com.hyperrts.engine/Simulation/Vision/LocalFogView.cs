using Unity.Entities;

namespace HyperRTS.Simulation.Vision
{
    /// <summary>Singleton: who the local player sees the fog as. Inactive (nothing hidden) without fog or a local team.</summary>
    public struct LocalFogView : IComponentData
    {
        public byte Viewer;
        public byte Team;
        public bool Active;

        /// <summary>Bumped on every restamp and whenever the viewer or activity changes.</summary>
        public int Version;
    }
}
