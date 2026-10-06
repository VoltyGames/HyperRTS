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
                // Movement never consults the grid for aircraft; this only serves spot searches (unloading).
                case NavLayer.Air:
                    return NavSurface.Land | NavSurface.Water | NavSurface.Deck;
                default:
                    return NavSurface.Land;
            }
        }

        /// <summary>
        /// Whether two agents can be in each other's way: a ship and a tank on a bridge above it can't, and aircraft
        /// only meet other aircraft.
        /// </summary>
        public static bool Share(NavLayer a, NavLayer b)
        {
            if (a == NavLayer.Air || b == NavLayer.Air)
            {
                return a == b;
            }

            return (Surfaces(a) & Surfaces(b)) != 0;
        }
    }
}
