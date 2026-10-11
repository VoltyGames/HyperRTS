namespace HyperRTS.Simulation.Combat
{
    /// <summary>What a shot struck where it landed; impact marks only go on open ground.</summary>
    public enum ImpactSurface : byte
    {
        Ground = 0,

        /// <summary>A river, lake or sea, including a ford's wading deck.</summary>
        Water = 1,

        /// <summary>A building, or a deck above the water (a bridge).</summary>
        Structure = 2,
        Air = 3,
    }
}
