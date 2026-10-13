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

        /// <summary>Whether the local player sees the impact: all of them without a local fog view.</summary>
        public static bool IsSeenLocally(in ImpactEvent impact, in LocalFogView view, in FogOfWar fog,
            in FactionRelations relations)
        {
            if (!view.Active)
            {
                return true;
            }

            return IsVisible(impact, view.Viewer, fog, relations);
        }
    }
}
