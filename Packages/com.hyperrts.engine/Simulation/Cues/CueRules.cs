using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Vision;

namespace HyperRTS.Simulation.Cues
{
    /// <summary>Who gets a cue, shared by the simulation, the network and playback.</summary>
    public static class CueRules
    {
        public static bool IsVoice(CueSlot slot) =>
            slot is CueSlot.Ready or CueSlot.Select or CueSlot.Move or CueSlot.Attack or CueSlot.AbilityVoice
                or CueSlot.UnderFire;

        public static bool IsFire(CueSlot slot) =>
            slot is CueSlot.Fire or CueSlot.Fire2 or CueSlot.Fire3 or CueSlot.Fire4;

        public static bool IsAnnouncement(CueSlot slot) =>
            slot is >= CueSlot.AnnounceUnitLost and <= CueSlot.AnnounceUpgradeComplete;

        /// <summary>Voices, production done and announcements speak only to their owner.</summary>
        public static bool IsOwnerOnly(CueSlot slot) =>
            IsVoice(slot) || IsAnnouncement(slot) || slot == CueSlot.ProductionDone;

        /// <summary>A shot that landed on the ground, the only impact that marks or deforms terrain.</summary>
        public static bool IsGroundImpact(in CueEvent cue) =>
            cue.Slot == CueSlot.Impact && cue.Surface == ImpactSurface.Ground;

        /// <summary>
        /// Owner-only cues reach their owner; everything else reaches whoever could see its source there, by the same
        /// fog and stealth rule as entities. Without a fog grid every non-voice cue does.
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

            return !fog.IsHiddenFrom(relations, viewer, cue.Faction, cue.Position, cue.Stealthed);
        }

        /// <summary>
        /// Whether the local player gets the cue: their own owner-only cues, and every other cue while the fog view is
        /// inactive (no fog, or everything revealed).
        /// </summary>
        public static bool IsSeenLocally(in CueEvent cue, in LocalFogView view, in FogOfWar fog,
            in FactionRelations relations)
        {
            if (IsOwnerOnly(cue.Slot))
            {
                return cue.Faction == view.Viewer;
            }

            if (!view.Active)
            {
                return true;
            }

            return IsVisible(cue, view.Viewer, fog, relations);
        }
    }
}
