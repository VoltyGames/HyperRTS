namespace HyperRTS.Presentation.Environment
{
    /// <summary>Resolution of the interaction maps (trampling, track marks).</summary>
    public enum InteractionQuality
    {
        /// <summary>No maps: grass stands and the ground stays clean.</summary>
        Off,

        /// <summary>Twice the texel size of <see cref="Full"/>.</summary>
        Low,

        Full,
    }
}
