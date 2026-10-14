using HyperRTS.Simulation.Common;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace HyperRTS.Simulation.Cues
{
    /// <summary>One effect rig per slot, played at a socket; a slot without an entry shows nothing.</summary>
    [InternalBufferCapacity(0)]
    public struct EntityEffect : IBufferElementData
    {
        public CueSlot Slot;

        /// <summary>A rig prefab (an <c>EffectRig</c> root with particle systems).</summary>
        public UnityObjectRef<GameObject> Rig;

        /// <summary>Socket position in the entity's local space (a muzzle, an exhaust).</summary>
        public float3 Socket;
    }
}
