namespace HyperRTS.Presentation.Effects
{
    /// <summary>How many particles effects emit and which extras they get.</summary>
    public enum EffectDetailLevel
    {
        /// <summary>Thinned particles, no flash lights, loops only near the camera.</summary>
        Low,

        /// <summary>Most particles, no flash lights.</summary>
        Medium,

        /// <summary>Full particles and brief flash lights.</summary>
        High,
    }
}
