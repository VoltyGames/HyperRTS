using Unity.Entities;

namespace HyperRTS.Simulation.Cues
{
    /// <summary>One cue per slot and variant; a slot without an entry is silent.</summary>
    [InternalBufferCapacity(0)]
    public struct EntitySound : IBufferElementData
    {
        public CueSlot Slot;

        /// <summary>Matches <see cref="CueEvent.Variant"/>, e.g. which weapon a fire cue is for.</summary>
        public byte Variant;

        public UnityObjectRef<SoundCue> Cue;

        public readonly bool Plays(CueSlot slot, byte variant) => Slot == slot && Variant == variant;
    }
}
