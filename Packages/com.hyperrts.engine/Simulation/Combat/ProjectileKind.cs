using System;

namespace HyperRTS.Simulation.Combat
{
    /// <summary>What a projectile is to an <see cref="Interceptor"/>, which engages any mix of these; None is never engaged.</summary>
    [Flags]
    public enum ProjectileKind : byte
    {
        None = 0,
        Shell = 1,
        Rocket = 2,
        Missile = 4,
        Bomb = 8,
    }
}
