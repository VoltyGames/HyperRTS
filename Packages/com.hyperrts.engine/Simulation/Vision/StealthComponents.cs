using Unity.Entities;
using Unity.NetCode;

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

    /// <summary>Enabled while stealth hides the entity right now: invisible and untargetable to undetecting enemies.</summary>
    [GhostEnabledBit]
    public struct Stealthed : IComponentData, IEnableableComponent { }

    /// <summary>Detection radius stamped into <see cref="FogOfWar.Detected"/> for the owner's team.</summary>
    public struct Detector : IComponentData
    {
        [GhostField] public float Radius;
    }
}
