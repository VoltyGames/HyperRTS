namespace HyperRTS.Simulation.Navigation
{
    /// <summary>What ground a building's footprint must cover (see <see cref="PlacementMath"/>).</summary>
    public enum PlacementSurface : byte
    {
        Land = 0,
        Water = 1,

        /// <summary>Touches both open land and open water (a naval yard, a coastal battery).</summary>
        Shoreline = 2,
    }
}
