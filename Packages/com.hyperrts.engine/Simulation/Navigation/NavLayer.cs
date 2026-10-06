namespace HyperRTS.Simulation.Navigation
{
    /// <summary>Which <see cref="NavSurface"/>s a <see cref="NavAgent"/> may move over (see <see cref="NavLayers"/>).</summary>
    public enum NavLayer : byte
    {
        Ground = 0,
        Naval = 1,

        /// <summary>Moves over land and water alike (hovercraft, amphibious vehicles).</summary>
        Amphibious = 2,

        /// <summary>Flies straight over everything at its <c>Flight</c> altitude, ignoring the grid.</summary>
        Air = 3,
    }
}
