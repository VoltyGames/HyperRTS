using Unity.Mathematics;

namespace HyperRTS.Simulation.Navigation
{
    /// <summary>
    /// The rules cells take their surface by after the ground (<see cref="NavGrid.GroundSurface"/>): the slope limit
    /// and nav areas. Editor tools measure maps with the same rules.
    /// </summary>
    public static class NavGround
    {
        /// <summary>Rise per metre of the steepest walkable ground; no limit for 0 or 90 degrees and above.</summary>
        public static float MaxGradient(float maxSlope) =>
            maxSlope > 0f && maxSlope < 90f ? math.tan(math.radians(maxSlope)) : float.PositiveInfinity;

        /// <summary>The surface a nav area leaves on a cell; a deck keeps any water under it open to ships.</summary>
        public static NavSurface Stamp(NavAreaKind kind, NavSurface cell) => kind switch
        {
            NavAreaKind.Water => NavSurface.Water,
            NavAreaKind.Blocked => NavSurface.Blocked,
            _ => (cell & NavSurface.Water) | NavSurface.Land | NavSurface.Deck,
        };
    }
}
