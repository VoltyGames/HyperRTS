using HyperRTS.Simulation.Navigation.Areas;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Navigation.Grid
{
    /// <summary>
    /// The slope limit and nav area rules that set a cell's surface after the ground
    /// (<see cref="NavGrid.GroundSurface"/>), shared with editor tools that measure maps, and the footing a body
    /// finds under each of its points.
    /// </summary>
    public static class NavGround
    {
        /// <summary>Rise per metre of the steepest walkable ground; no limit for 0 or 90 degrees and above.</summary>
        public static float MaxGradient(float maxSlope) =>
            maxSlope > 0f && maxSlope < 90f ? math.tan(math.radians(maxSlope)) : float.PositiveInfinity;

        /// <summary>
        /// The ground or deck under <paramref name="point"/> of a body whose centre stands at <paramref name="center"/>:
        /// never below the deck the centre stands on, since the terrain under a bridge is the valley it spans.
        /// </summary>
        public static float Footing(in NavGrid grid, float2 point, float2 center)
        {
            if (grid.TryDeckHeight(point, out var deck))
            {
                return deck;
            }

            var ground = grid.Terrain.Height(point);
            return grid.TryDeckHeight(center, out var standing) ? math.max(ground, standing) : ground;
        }

        /// <summary>The surface a nav area leaves on a cell; a deck keeps any water under it open to ships.</summary>
        public static NavSurface Stamp(NavAreaKind kind, NavSurface cell) => kind switch
        {
            NavAreaKind.Water => NavSurface.Water,
            NavAreaKind.Blocked => NavSurface.Blocked,
            _ => (cell & NavSurface.Water) | NavSurface.Land | NavSurface.Deck,
        };
    }
}
