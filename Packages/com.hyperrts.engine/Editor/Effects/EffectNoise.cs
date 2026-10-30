using System;

namespace HyperRTS.Editor.Effects
{
    /// <summary>A smooth wandering push on a layer's particles, as Shuriken's Noise module; zero strength is none.</summary>
    [Serializable]
    public struct EffectNoise
    {
        /// <summary>Metres a second at the field's peak.</summary>
        public float strength;

        /// <summary>Waves a metre.</summary>
        public float frequency;

        /// <summary>How fast the field moves through the particles, in waves a second.</summary>
        public float scroll;

        public bool IsSet => strength > 0f;
    }
}
