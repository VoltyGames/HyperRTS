namespace HyperRTS.Simulation.Combat.Weapons
{
    /// <summary>How a weapon chooses what to fire at, baked from its targets and whether it fires on order.</summary>
    public enum WeaponRole : byte
    {
        /// <summary>Picks hostile units and buildings itself, and takes ordered ones.</summary>
        AutoEngage,

        /// <summary>Never picks targets itself: fires only at a ground point or a unit it is ordered onto.</summary>
        OnOrder,

        /// <summary>Shoots projectiles in flight, such as a C-RAM gun, and nothing else.</summary>
        Interceptor,
    }
}
