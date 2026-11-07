using HyperRTS.Simulation.Common;



namespace HyperRTS.Presentation.Quality
{
    /// <summary>
    /// Particle share per effect level. Low thins particles and keeps loops near the camera; only High adds flash
    /// lights.
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
    }
}
