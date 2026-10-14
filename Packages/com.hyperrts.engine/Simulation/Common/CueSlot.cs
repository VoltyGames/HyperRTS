namespace HyperRTS.Simulation.Common
{
    /// <summary>
    /// What an entity's sound and effect are for. Voice slots (<see cref="Ready"/> up to <see cref="Custom"/>) reach
    /// only the owner, and <see cref="Impact"/> is sound only. Games add their own slots from <see cref="Custom"/> up.
    /// </summary>
    public enum CueSlot : byte
    {
        None = 0,

        /// <summary>Each shot, at the muzzle.</summary>
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
