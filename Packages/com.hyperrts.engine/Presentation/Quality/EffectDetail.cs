using HyperRTS.Simulation.Common;

namespace HyperRTS.Presentation.Quality
{
    /// <summary>
    /// Particle share, ragdoll count and corpses a frame per effect level. Low thins particles, keeps loops near the
    /// camera and has no ragdolls; only High adds flash lights.
    /// </summary>
    public static class EffectDetail
    {
        /// <summary>The share of authored particles emitted at the current level.</summary>
        public static float Density => DensityAt(PresentationQuality.Current.Effects);

        public static float DensityAt(DetailLevel level) => level switch
        {
            DetailLevel.Low => 0.35f,
            DetailLevel.Medium => 0.7f,
            _ => 1f,
        };

        /// <summary>Most figures that are ragdolls at once at <paramref name="level"/>: the live pose slots kept.</summary>
        public static int RagdollsAt(DetailLevel level) => level switch
        {
            DetailLevel.Low => 0,
            DetailLevel.Medium => 24,
            _ => 48,
        };

        /// <summary>
        /// Most corpses made or handed over to a ragdoll in one frame at <paramref name="level"/>, so a blast that kills
        /// hundreds spreads their cost.
        /// </summary>
        public static int CorpsesPerFrameAt(DetailLevel level) => level switch
        {
            DetailLevel.Low => 8,
            DetailLevel.Medium => 16,
            _ => 24,
        };
    }
}
