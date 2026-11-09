namespace HyperRTS.Simulation.Buildings.Placement
{
    /// <summary>How a <see cref="PlacementLine"/>'s first segment sits at its start; a PlaceLine command's Argument.</summary>
    public enum LineStart
    {
        /// <summary>A new line: the first segment snaps to the grid.</summary>
        Snapped = 0,

        /// <summary>
        /// Joined to the end of the line before at a corner: the start is exact, from
        /// <see cref="PlacementLine.JoinedStart"/>.
        /// </summary>
        Joined = 1,
    }
}
