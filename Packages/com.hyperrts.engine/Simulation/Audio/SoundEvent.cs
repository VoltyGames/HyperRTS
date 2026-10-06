using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Audio
{
    /// <summary>
    /// A sound to play this frame, on the <see cref="SoundQueue"/> singleton. It names the source by type and slot
    /// rather than by asset, so a client resolves it through its own copy of the prefab.
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct SoundEvent : IBufferElementData
    {
        /// <summary><c>EntityInfo.TypeId</c> of the entity whose <see cref="EntitySound"/> cue plays.</summary>
        public int TypeId;

        public SoundSlot Slot;

        /// <summary>Owner of the source; decides who hears it through fog and voice rules.</summary>
        public byte Faction;

        public float3 Position;
    }

    /// <summary>Tags the singleton holding this frame's <see cref="SoundEvent"/>s.</summary>
    public struct SoundQueue : IComponentData { }
}
