namespace HyperRTS.Simulation.Common
{
    /// <summary>Values a <see cref="StatModifier"/> can change.</summary>
    public enum Stat : byte
    {
        MaxHealth = 0,
        Damage = 1,
        Range = 2,

        /// <summary>
        /// Shots per second: Add is extra shots per second and +100% halves the cooldown. Its recorded base is the
        /// cooldown.
        /// </summary>
        FireRate = 3,
        MoveSpeed = 4,
        VisionRange = 5,

        /// <summary>Incoming damage multiplier (base 1); -25% takes a quarter less.</summary>
        DamageTaken = 6,
        BuildRate = 7,
        ProductionSpeed = 8,

        /// <summary>On a player: resources its harvesters deliver, as a multiplier (base 1).</summary>
        Income = 9,
    }
}
