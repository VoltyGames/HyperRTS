using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Vision.Fog;
using HyperRTS.Simulation.Vision.Sight;

namespace HyperRTS.Simulation.Cues
{
    /// <summary>Who gets a cue, shared by the simulation, the network and local playback.</summary>
    public static class CueRules
    {
        private static bool IsVoice(CueSlot slot) =>
            slot is CueSlot.Ready or CueSlot.Select or CueSlot.Move or CueSlot.Attack or CueSlot.AbilityVoice
                or CueSlot.UnderFire or CueSlot.TakeCover or CueSlot.Suppressed or CueSlot.Pinned or CueSlot.Retreat
                or CueSlot.DigIn or CueSlot.Reinforced;

        /// <summary>Voices, production done and announcements speak only to their owner.</summary>
        public static bool IsOwnerOnly(CueSlot slot) =>
            IsVoice(slot) || slot is CueSlot.Announce or CueSlot.ProductionDone;

        /// <summary>
        /// Owner-only cues reach their owner; everything else reaches whoever could see its source there, by the same
        /// fog and cover rule as entities. Without a fog grid every non-voice cue does.
        /// </summary>
        public static bool IsVisible(in CueEvent cue, byte viewer, in FogOfWar fog, in FactionRelations relations)
        {
            if (IsOwnerOnly(cue.Slot))
            {
                return cue.Faction == viewer;
            }

            if (!fog.IsCreated)
            {
                return true;
            }

            return !fog.IsHiddenFrom(relations, viewer, cue.Faction, cue.Position, cue.Concealment, cue.Tall);
        }
    }
}
