using Unity.Mathematics;

namespace HyperRTS.Simulation.Navigation
{
    /// <summary>
    /// How cells get their surface: from the ground (flooded terrain under the water level is water, land steeper
    /// than the slope limit is blocked), then from nav areas. Editor tools measure maps with the same rules.
    /// </summary>
    public static class NavGround
    {
        /// <summary>Rise per metre of the steepest walkable ground; no limit for 0 or 90 degrees and above.</summary>
        public static float MaxGradient(float maxSlope) =>
            maxSlope > 0f && maxSlope < 90f ? math.tan(math.radians(maxSlope)) : float.PositiveInfinity;

        public static NavSurface Classify(in TerrainHeight terrain, float2 center, float cellSize, float waterLevel,
            bool flood, float maxGradient)
        {
            if (flood && terrain.Height(center) < waterLevel)
            {
                return NavSurface.Water;
            }

            var half = cellSize * 0.5f;
            var dx = terrain.Height(center + new float2(half, 0f)) - terrain.Height(center - new float2(half, 0f));
            var dz = terrain.Height(center + new float2(0f, half)) - terrain.Height(center - new float2(0f, half));
            return math.length(new float2(dx, dz)) > maxGradient * cellSize ? NavSurface.Blocked : NavSurface.Land;
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
