namespace HyperRTS.Presentation.Quality
{
    /// <summary>Resolution of the trample maps (trampling, track marks).</summary>
    public enum TrampleQuality
    {
        /// <summary>No maps: grass stands and the ground stays clean.</summary>
        Off,

        /// <summary>Twice the texel size of <see cref="Full"/>.</summary>
        Low,

        Full,
    }
}
