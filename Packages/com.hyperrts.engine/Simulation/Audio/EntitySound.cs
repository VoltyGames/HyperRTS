using HyperRTS.Simulation.Common;
using Unity.Entities;

namespace HyperRTS.Simulation.Audio
{
    /// <summary>One cue per slot; a slot without an entry is silent.</summary>
    [InternalBufferCapacity(0)]
    public struct EntitySound : IBufferElementData
    {
        public CueSlot Slot;
        public UnityObjectRef<SoundCue> Cue;
    }
}
