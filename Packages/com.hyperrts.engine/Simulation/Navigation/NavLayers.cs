namespace HyperRTS.Simulation.Navigation
{
    /// <summary>Maps <see cref="NavLayer"/>s to the <see cref="NavSurface"/>s they may move over.</summary>
    public static class NavLayers
    {
        public static NavSurface Surfaces(NavLayer layer)
        {
            switch (layer)
            {
                case NavLayer.Naval:
                    return NavSurface.Water;
                case NavLayer.Amphibious:
                    return NavSurface.Land | NavSurface.Water;
                default:
                    return NavSurface.Land;
            }
        }

        /// <summary>Whether two agents can be in each other's way (a ship and a tank on a bridge above it can't).</summary>
        public static bool Share(NavLayer a, NavLayer b) => (Surfaces(a) & Surfaces(b)) != 0;
    }
}
