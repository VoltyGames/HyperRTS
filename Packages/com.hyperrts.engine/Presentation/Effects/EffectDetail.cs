using HyperRTS.Presentation.Rendering;
using HyperRTS.Simulation.Common;
using Unity.Scripting.LifecycleManagement;

namespace HyperRTS.Presentation.Effects
{
    /// <summary>
    /// Effect detail, written by <see cref="PresentationQuality.Apply"/>. Low thins particles and keeps loops near
    /// the camera; only High adds flash lights.
    /// </summary>
    public static partial class EffectDetail
    {
        [AutoStaticsCleanup]
        public static DetailLevel Level { get; internal set; } = PresentationQuality.Default.Effects;

        /// <summary>The share of authored particles emitted at the current level.</summary>
        public static float Density => DensityAt(Level);

        public static float DensityAt(DetailLevel level) => level switch
        {
            DetailLevel.Low => 0.35f,
            DetailLevel.Medium => 0.7f,
            _ => 1f,
        };

        /// <summary>Settled debris pieces kept for the match at <paramref name="level"/>.</summary>
        public static int LastingDebrisAt(DetailLevel level) => level switch
        {
            DetailLevel.Low => 0,
            DetailLevel.Medium => 8000,
            _ => 20000,
        };
    }
}
