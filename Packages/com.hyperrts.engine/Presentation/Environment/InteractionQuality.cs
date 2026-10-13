namespace HyperRTS.Presentation.Environment
{
    /// <summary>How finely the interaction maps (trampling, track marks) resolve the ground; the game sets it.</summary>
    public enum InteractionQuality
    {
        /// <summary>No maps: grass stands and the ground stays clean.</summary>
        Off,

        /// <summary>Twice the texel size of <see cref="Full"/>.</summary>
        Low,

        Full,
    }
}
