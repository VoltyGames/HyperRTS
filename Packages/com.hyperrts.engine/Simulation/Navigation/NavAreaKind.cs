namespace HyperRTS.Simulation.Navigation
{
    /// <summary>How a <see cref="NavArea"/> overrides the grid cells under it.</summary>
    public enum NavAreaKind : byte
    {
        /// <summary>Water for every layer's purposes: naval agents pass, ground agents don't.</summary>
        Water = 0,

        /// <summary>Closed to every layer.</summary>
        Blocked = 1,

        /// <summary>Walkable at the area's height (a bridge); water below stays open to ships.</summary>
        Deck = 2,
    }
}
