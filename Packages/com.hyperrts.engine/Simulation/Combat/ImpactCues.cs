using HyperRTS.Simulation.Cues;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Combat
{
    /// <summary>Impact cues from damage events; here, not in Cues, which sits below Combat.</summary>
    public static class ImpactCues
    {
        /// <summary>Reports where a shot of a shooter type landed; a type id of 0 is ignored.</summary>
        public static void AddImpact(this CueWriter cues, int typeId, in DamageEvent hit, float3 direction) =>
            cues.Add(typeId, CueSlot.Impact, hit.Position, direction, hit.SourceFaction, radius: hit.Radius,
                target: hit.Target);
    }
}
