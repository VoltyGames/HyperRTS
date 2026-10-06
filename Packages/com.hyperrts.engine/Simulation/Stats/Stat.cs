namespace HyperRTS.Simulation.Stats
{
    /// <summary>
    /// Values a <see cref="StatModifier"/> can change; the module owning each value applies it. Games add their own
    /// from <see cref="Custom"/> upward and apply them in their own systems with <see cref="StatMath.Apply"/>.
    /// </summary>
    public enum Stat : byte
    {
        MaxHealth = 0,
        Damage = 1,
        Range = 2,

        /// <summary>
        /// Shots per second: Add is extra shots per second and +100% halves the cooldown. Its captured base is the
        /// cooldown.
        /// </summary>
        FireRate = 3,
        MoveSpeed = 4,
        VisionRange = 5,

        /// <summary>Incoming damage multiplier (base 1); -25% takes a quarter less.</summary>
        DamageTaken = 6,
        BuildRate = 7,
        ProductionSpeed = 8,
        Custom = 128,
    }
}
