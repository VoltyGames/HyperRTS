using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Vision;

namespace HyperRTS.Simulation.Cues
{
    /// <summary>Who gets a cue, shared by the simulation, the network and playback.</summary>
    public static class CueRules
    {
        /// <summary>Voices (produced, select, move, attack) speak only to their owner.</summary>
        public static bool IsVoice(CueSlot slot) =>
            slot is CueSlot.Ready or CueSlot.Select or CueSlot.Move or CueSlot.Attack;

        /// <summary>
        /// Voices reach their owner only; everything else reaches whoever could see its source there, by the same
        /// fog and stealth rule as entities. Without a fog grid every non-voice cue does.
        /// </summary>
        public static bool IsVisible(in CueEvent cue, byte viewer, in FogOfWar fog, in FactionRelations relations)
        {
            if (IsVoice(cue.Slot))
            {
                return cue.Faction == viewer;
            }

            if (!fog.IsCreated)
            {
                return true;
            }

            return !fog.IsHiddenFrom(relations, viewer, cue.Faction, cue.Position, cue.Stealthed);
        }

        /// <summary>Whether the local player gets the cue: all of them without a local fog view.</summary>
        public static bool IsSeenLocally(in CueEvent cue, in LocalFogView view, in FogOfWar fog,
            in FactionRelations relations)
        {
            if (!view.Active)
            {
                return true;
            }

            return IsVisible(cue, view.Viewer, fog, relations);
        }
    }
}
