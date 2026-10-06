namespace HyperRTS.Simulation.Stats
{
    /// <summary>Values a <see cref="StatModifier"/> can change.</summary>
    public enum Stat : byte
    {
        MaxHealth = 0,
        Damage = 1,
        Range = 2,

        /// <summary>Shots per second; +100% halves the weapon cooldown.</summary>
        FireRate = 3,
        MoveSpeed = 4,
        VisionRange = 5,

        /// <summary>Incoming damage multiplier (base 1); -25% takes a quarter less.</summary>
        DamageTaken = 6,
        BuildRate = 7,
        ProductionSpeed = 8,
    }
}
