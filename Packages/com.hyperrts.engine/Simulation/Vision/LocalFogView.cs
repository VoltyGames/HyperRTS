using Unity.Entities;

namespace HyperRTS.Simulation.Vision
{
    /// <summary>
    /// Singleton: who the local player sees the fog as. Inactive (nothing hidden) without fog or a local team, and
    /// while <see cref="RevealAll"/>.
    /// </summary>
    public struct LocalFogView : IComponentData
    {
        public byte Viewer;
        public byte Team;
        public bool Active;

        /// <summary>Spectating (e.g. a replay) shows everything; set through <see cref="LocalFogViewSystem.SetRevealAll"/>.</summary>
        public bool RevealAll;

        /// <summary>Bumped on every restamp and whenever the viewer or activity changes.</summary>
        public int Version;
    }
}
