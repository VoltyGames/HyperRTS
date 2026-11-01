using HyperRTS.Simulation.Common;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Simulation.Combat.Death
{
    /// <summary>A damageable entity's <see cref="IModelStage"/> by health, from intact to wrecked.</summary>
    [InternalBufferCapacity(3)]
    public struct DamageStage : IModelStage
    {
        public float Below;
        public Entity Visual;

        /// <summary>Played where the entity stands when it drops into this stage (dust, debris); may be empty.</summary>
        public UnityObjectRef<GameObject> Effect;

        float IModelStage.Below => Below;

        Entity IModelStage.Visual => Visual;
    }

    /// <summary>The <see cref="DamageStage"/> on screen; presentation keeps it, -1 until the first frame shows one.</summary>
    public struct ShownDamageStage : IComponentData
    {
        public int Index;
    }
}
