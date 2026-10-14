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

    /// <summary>A rig that streams while the entity is shown: exhaust, chimney smoke, a burning wreck.</summary>
    [InternalBufferCapacity(0)]
    public struct LoopEffect : IBufferElementData
    {
        public UnityObjectRef<GameObject> Rig;

        /// <summary>Socket position in the entity's local space.</summary>
        public float3 Socket;

        /// <summary>Streams only while the health fraction is above this (a chimney stops once hit); 0 always.</summary>
        public float AboveHealth;

        /// <summary>Seconds it streams after the entity is first shown, then fades out; 0 forever.</summary>
        public float Duration;
    }

    /// <summary>A rig streamed along a projectile's path (smoke, a tracer glow).</summary>
    public struct EffectTrail : IComponentData
    {
        public UnityObjectRef<GameObject> Rig;
    }
}
