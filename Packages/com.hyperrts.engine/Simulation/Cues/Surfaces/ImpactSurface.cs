namespace HyperRTS.Simulation.Cues.Surfaces
{
    /// <summary>
    /// Where a shot landed, as opposed to the <see cref="SurfaceMaterial"/> of what it struck; impact marks only go on
    /// open ground.
    /// </summary>
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
