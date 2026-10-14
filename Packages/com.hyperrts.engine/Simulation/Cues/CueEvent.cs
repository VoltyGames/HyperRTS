using HyperRTS.Simulation.Common;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Cues
{
    /// <summary>
    /// A sound and effect to play this frame, on the <see cref="CueQueue"/> singleton. It names the source by type and
    /// slot, so a client resolves the type's <c>EntitySound</c> and <c>EntityEffect</c> through its own prefab copy.
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct CueEvent : IBufferElementData
    {
        public int TypeId;
        public CueSlot Slot;

        /// <summary>Owner of the source; decides who hears and sees it through fog and voice rules.</summary>
        public byte Faction;

        /// <summary>World position of the slot's effect socket, else of the source.</summary>
        public float3 Position;

        /// <summary>Unit vector the effect faces, e.g. along the shot.</summary>
        public float3 Direction;

        /// <summary>The source was stealthed: only teams detecting it get the cue.</summary>
        public bool Stealthed;
    }

    /// <summary>Tags the singleton holding this frame's <see cref="CueEvent"/>s.</summary>
    public struct CueQueue : IComponentData { }
}
