using HyperRTS.Simulation.Common;
using Unity.Entities;

namespace HyperRTS.Simulation.Audio
{
    /// <summary>Adds an entity's sound cues.</summary>
    public static class SoundSetup
    {
        public static DynamicBuffer<EntitySound> Add<TWriter>(ref TWriter writer)
            where TWriter : struct, IEntityWriter =>
            writer.AddBuffer<EntitySound>();

        /// <summary>Unset cues are skipped, so the slot stays silent and writes no events.</summary>
        public static void Set(DynamicBuffer<EntitySound> sounds, SoundSlot slot, SoundCue cue)
        {
            if (cue != null)
            {
                sounds.Add(new EntitySound { Slot = slot, Cue = cue });
            }
        }
    }
}
