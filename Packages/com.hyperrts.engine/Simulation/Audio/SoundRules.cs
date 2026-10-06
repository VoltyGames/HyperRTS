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

        public static bool TryGetCue(DynamicBuffer<EntitySound> sounds, SoundSlot slot, out UnityObjectRef<SoundCue> cue)
        {
            foreach (var sound in sounds)
            {
                if (sound.Slot == slot)
                {
                    cue = sound.Cue;
                    return true;
                }
            }

            cue = default;
            return false;
        }

        /// <summary>
        /// Voices reach their owner only; everything else reaches whoever can see where it happens. Without fog
        /// (<paramref name="fogActive"/> false) every non-voice event is audible.
        /// </summary>
        public static bool IsAudible(in SoundEvent sound, byte listener, bool fogActive, in FogOfWar fog,
            in FactionRelations relations)
        {
            if (IsVoice(sound.Slot))
            {
                return sound.Faction == listener;
            }

            if (!fogActive)
            {
                return true;
            }

            return !fog.IsHiddenFrom(relations, listener, sound.Faction, sound.Position);
        }
    }
}
