using System;

namespace HyperRTS.Simulation.Combat.Weapons
{
    /// <summary>
    /// What a weapon can hit, as a set of target classes. A unit's class comes from its nav layer; buildings count as
    /// Ground.
    /// </summary>
    [Flags]
    public enum WeaponTargets : byte
    {
        None = 0,
        Ground = 1,
        Naval = 2,
        Air = 4,

        /// <summary>Shells, rockets and missiles in flight, for interceptors.</summary>
        Projectile = 8,

        /// <summary>Ground and naval units and buildings.</summary>
        Surface = Ground | Naval,
    }
}
