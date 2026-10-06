using Unity.Entities;

namespace HyperRTS.Simulation.Vision
{
    /// <summary>
    /// Stealth rules of an entity. Enabled while it has stealth at all, which games toggle at runtime;
    /// <see cref="StealthSystem"/> turns that into <see cref="Stealthed"/>.
    /// </summary>
    public struct Stealth : IComponentData, IEnableableComponent
    {
        public float RevealDuration;
        public bool OnlyWhenStill;

        /// <summary>Seconds left visible after firing.</summary>
        public float RevealTimer;
    }
}
