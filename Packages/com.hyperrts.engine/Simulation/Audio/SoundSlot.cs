namespace HyperRTS.Simulation.Audio
{
    /// <summary>
    /// What an entity's sound is for. Voice slots (<see cref="Ready"/> and up to <see cref="Custom"/>) are heard only
    /// by the owner. Games add their own slots from <see cref="Custom"/> upward.
    /// </summary>
    public enum SoundSlot : byte
    {
        None = 0,
        Fire = 1,
        Impact = 2,
        Death = 3,
        Ability = 4,

        /// <summary>Produced, or construction finished.</summary>
        Ready = 5,
        Select = 6,
        Move = 7,
        Attack = 8,
        Custom = 64,
    }
}
