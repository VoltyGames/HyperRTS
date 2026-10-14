using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Effects
{
    /// <summary>
    /// An effect to play this frame, on the <see cref="EffectQueue"/> singleton. It names the source by type and slot,
    /// so a client resolves the rig through its own copy of the prefab.
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct EffectEvent : IBufferElementData
    {
        /// <summary><c>EntityInfo.TypeId</c> of the entity whose <see cref="EntityEffect"/> rig plays.</summary>
        public int TypeId;

        public EffectSlot Slot;

        /// <summary>Owner of the source; decides who sees it through fog.</summary>
        public byte Faction;

        /// <summary>World position of the slot's socket.</summary>
        public float3 Position;

        /// <summary>Unit vector the effect faces, e.g. along the shot.</summary>
        public float3 Direction;

        public bool Stealthed;
    }

    /// <summary>Tags the singleton holding this frame's <see cref="EffectEvent"/>s.</summary>
    public struct EffectQueue : IComponentData { }
}
