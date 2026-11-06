using System;

namespace HyperRTS.Simulation.Combat.Projectiles
{
    /// <summary>What a projectile is to an interceptor weapon, which engages any mix of these; None is never engaged.</summary>
    [Flags]
    public enum ProjectileKind : byte
    {
        None = 0,
        Shell = 1,
        Rocket = 2,
        Missile = 4,
        Bomb = 8,

        /// <summary>First flag a game may use for its own kinds (16, 32, 64, 128); the engine never defines one from here up.</summary>
        GameFirst = 16,
    }
}
