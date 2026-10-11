using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Vision;

namespace HyperRTS.Simulation.Combat
{
    /// <summary>Who sees an impact, shared by the network and presentation.</summary>
    public static class ImpactRules
    {
        /// <summary>
        /// Whoever could see a unit standing there sees the impact (the fog rule of entities): the shooter's own side
        /// always, enemies only in sight. Without a fog grid everyone does.
        /// </summary>
        public static bool IsVisible(in ImpactEvent impact, byte viewer, in FogOfWar fog, in FactionRelations relations) =>
            !fog.IsCreated || !fog.IsHiddenFrom(relations, viewer, impact.Faction, impact.Position, false);
    }
}
