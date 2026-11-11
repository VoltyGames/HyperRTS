using HyperRTS.Simulation.Common.Buildings;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Players;
using Unity.Entities;

namespace HyperRTS.Simulation.Air
{
    /// <summary>Which airfields an aircraft may use, shared by orders, pads and production.</summary>
    public static class AirfieldRules
    {
        /// <summary>A finished building with landing pads that <paramref name="owner"/> owns.</summary>
        public static bool IsOwnedAirfield(in BufferLookup<LandingPad> pads, in ComponentLookup<Owner> owners,
            in ComponentLookup<ConstructionProgress> sites, Entity airfield, byte owner)
        {
            if (!pads.HasBuffer(airfield) || sites.HasEnabled(airfield))
            {
                return false;
            }

            return owners.TryGetComponent(airfield, out var airfieldOwner) && airfieldOwner.Value == owner;
        }
    }
}
