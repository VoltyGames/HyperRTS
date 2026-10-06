using HyperRTS.Simulation.Buildings;
using HyperRTS.Simulation.Match;
using Unity.Entities;

namespace HyperRTS.Simulation.Air
{
    /// <summary>Which airfields an aircraft may use, shared by orders, pads and production.</summary>
    public static class AirfieldRules
    {
        /// <summary>A finished building with landing pads that <paramref name="faction"/> owns.</summary>
        public static bool IsAirfieldOf(in BufferLookup<LandingPad> pads, in ComponentLookup<Faction> factions,
            in ComponentLookup<ConstructionProgress> sites, Entity airfield, byte faction)
        {
            if (!pads.HasBuffer(airfield) || ConstructionRules.IsUnderConstruction(sites, airfield))
            {
                return false;
            }

            return factions.TryGetComponent(airfield, out var owner) && owner.Value == faction;
        }
    }
}
