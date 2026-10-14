using UnityEngine;

namespace HyperRTS.Presentation.Effects
{
    /// <summary>Effect detail, set by the game from its effects quality.</summary>
    public static class EffectDetail
    {
        public static EffectDetailLevel Level { get; set; } = EffectDetailLevel.High;

        /// <summary>The share of authored particles emitted at the current level.</summary>
        public static float Density => DensityAt(Level);

        public static float DensityAt(EffectDetailLevel level) => level switch
        {
            EffectDetailLevel.Low => 0.35f,
            EffectDetailLevel.Medium => 0.7f,
            _ => 1f,
        };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Level = EffectDetailLevel.High;
    }
}
