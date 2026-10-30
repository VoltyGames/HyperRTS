using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Cues
{
    /// <summary>What a death leaves on the ground for the rest of the match, such as fuel and wet mud under a hull.</summary>
    public struct DeathRemains : IComponentData
    {
        /// <summary>Metres from where it died.</summary>
        public float Radius;

        /// <summary>Rubble, ash, fuel and wet mud, 0 to 1 each.</summary>
        public float4 Amounts;
    }
}
