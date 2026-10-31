using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Presentation.Environment.Weather
{
    /// <summary>The scene's wind, published by <see cref="Wind"/> as a singleton for the presentation systems; absent means still air.</summary>
    public struct WindState : IComponentData
    {
        /// <summary>The downwind direction on the ground (xz), unit length.</summary>
        public float3 Downwind;

        /// <summary>The WindZone's main strength.</summary>
        public float Strength;

        /// <summary>Metres per second the wind pushes things along the ground (xz).</summary>
        public readonly float2 Push => Downwind.xz * Strength;
    }
}
