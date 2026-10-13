using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Vision;
using Unity.Mathematics;

namespace HyperRTS.Presentation.Fog
{
    /// <summary>
    /// Whether a building's markings (flag, capture dial, garrison pips) hide from the local player. Map buildings are
    /// never fog-hidden themselves, so who holds them shows only where the player sees or for his own side.
    /// </summary>
    public static class BuildingFog
    {
        public static bool IsHidden(in LocalFogView view, in FogOfWar fog, in FactionRelations relations, byte owner,
            float3 position)
        {
            if (!view.Active)
            {
                return false;
            }

            var friendly = relations.IsAllied(view.Viewer, owner);
            return !friendly && !fog.IsVisible(position, view.Team);
        }
    }
}
