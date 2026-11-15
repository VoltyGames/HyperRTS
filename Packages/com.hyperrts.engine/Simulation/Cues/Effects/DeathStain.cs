using Unity.Entities;

namespace HyperRTS.Simulation.Cues.Effects
{
    /// <summary>What a death leaves on the ground for the rest of the match, such as fuel and wet mud under a hull.</summary>
    public struct DeathStain : IComponentData
    {
        /// <summary>Metres from where it died.</summary>
        public float Radius;

        public GroundScar Scar;
    }
}
