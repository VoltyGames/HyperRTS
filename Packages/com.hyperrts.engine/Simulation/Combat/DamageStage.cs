using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Simulation.Combat
{
    /// <summary>
    /// One look of a damageable entity: its <see cref="Visual"/> child shows while health is below
    /// <see cref="Below"/> (a fraction) and above the next stage's. Stages are ordered from intact to wrecked.
    /// </summary>
    [InternalBufferCapacity(3)]
    public struct DamageStage : IBufferElementData
    {
        public float Below;
        public Entity Visual;

        /// <summary>Played where the entity stands when it drops into this stage (dust, debris); may be empty.</summary>
        public UnityObjectRef<GameObject> Effect;
    }
}
