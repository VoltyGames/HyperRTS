using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace HyperRTS.Simulation.Effects
{
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
}
