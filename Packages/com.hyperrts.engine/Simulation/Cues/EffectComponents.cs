using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace HyperRTS.Simulation.Cues
{
    /// <summary>One effect rig per slot and variant, played at a socket; a slot without an entry shows nothing.</summary>
    [InternalBufferCapacity(0)]
    public struct EntityEffect : IBufferElementData
    {
        public CueSlot Slot;

        /// <summary>Matches <see cref="CueEvent.Variant"/>, e.g. which weapon a fire rig is for.</summary>
        public byte Variant;

        /// <summary>A rig prefab (an <c>EffectRig</c> root with particle systems).</summary>
        public UnityObjectRef<GameObject> Rig;

        /// <summary>Socket position in the entity's local space (a muzzle, an exhaust).</summary>
        public float3 Socket;

        public readonly bool Plays(CueSlot slot, byte variant) => Slot == slot && Variant == variant;
    }
}
