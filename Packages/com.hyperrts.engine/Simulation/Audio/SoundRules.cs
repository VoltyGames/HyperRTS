using HyperRTS.Simulation.Match;
using HyperRTS.Simulation.Vision;
using Unity.Entities;

namespace HyperRTS.Simulation.Audio
{
    /// <summary>Cue lookup and who may hear an event, shared by the simulation, the network and playback.</summary>
    public static class SoundRules
    {
        /// <summary>Voices (produced, select, move, attack) speak only to their owner.</summary>
        public static bool IsVoice(SoundSlot slot) => slot >= SoundSlot.Ready && slot < SoundSlot.Custom;

        public static bool HasCue(DynamicBuffer<EntitySound> sounds, SoundSlot slot)
        {
            foreach (var sound in sounds)
            {
                if (sound.Slot == slot)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Voices reach their owner only; everything else reaches whoever could see its source there, by the same
        /// fog and stealth rule as entities. Without a fog grid every non-voice event is audible.
        /// </summary>
        public static bool IsAudible(in SoundEvent sound, byte listener, in FogOfWar fog, in FactionRelations relations)
        {
            if (IsVoice(sound.Slot))
            {
                return sound.Faction == listener;
            }

            if (!fog.IsCreated)
            {
                return true;
            }

            return !fog.IsHiddenFrom(relations, listener, sound.Faction, sound.Position, sound.Stealthed);
        }
    }
}
