using UnityEngine;

namespace HyperRTS.Presentation.Effects
{
    /// <summary>Effect detail, set by the game from its effects quality.</summary>
    public static class EffectDetail
    {
        public static EffectDetailLevel Level { get; set; } = EffectDetailLevel.High;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Level = EffectDetailLevel.High;
    }
}
