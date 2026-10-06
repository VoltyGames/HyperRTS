namespace HyperRTS.Simulation.Combat
{
    /// <summary>What a weapon can hit: surface units and buildings (ground and naval), aircraft, or both.</summary>
    public enum WeaponTargets : byte
    {
        Surface = 0,
        Air = 1,
        SurfaceAndAir = 2,
    }
}
