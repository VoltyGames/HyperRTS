using Unity.Entities;

namespace HyperRTS.Simulation.Cues
{
    /// <summary>Cue lookup in an entity's sounds, shared by the simulation and playback.</summary>
    public static class SoundRules
    {
        public static bool HasCue(DynamicBuffer<EntitySound> sounds, CueSlot slot, byte variant)
        {
            foreach (var sound in sounds)
            {
                if (sound.Plays(slot, variant))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
