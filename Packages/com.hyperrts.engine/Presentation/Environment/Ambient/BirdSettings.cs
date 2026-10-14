using Unity.Mathematics;

namespace HyperRTS.Presentation.Environment.Ambient
{
    /// <summary>How a <see cref="BirdFlock"/> flies, from <see cref="AmbientLife"/>.</summary>
    public struct BirdSettings
    {
        /// <summary>Metres above the ground, min and max.</summary>
        public float2 Height;

        public float ScatterRadius;

        /// <summary>Seconds a scattered flock flies off before settling, min and max.</summary>
        public float2 ReturnDelay;
    }
}
