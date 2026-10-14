using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Simulation.Effects
{
    /// <summary>A rig streamed along a projectile's path (smoke, a tracer glow).</summary>
    public struct EffectTrail : IComponentData
    {
        public UnityObjectRef<GameObject> Rig;
    }
}
