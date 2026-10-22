using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Simulation.Combat
{
    /// <summary>
    /// One look of an entity that swaps child models by a fraction, such as health or what a resource node has left.
    /// Stages are ordered from whole to spent.
    /// </summary>
    public interface IModelStage : IBufferElementData
    {
        /// <summary>Shown while the fraction is at or below this and above the next stage's.</summary>
        float Below { get; }

        Entity Visual { get; }

        /// <summary>The most spent stage whose threshold <paramref name="fraction"/> is at or below.</summary>
        static int Pick<T>(NativeArray<T> stages, float fraction) where T : unmanaged, IModelStage
        {
            var current = 0;
            for (var i = 0; i < stages.Length; i++)
            {
                if (fraction <= stages[i].Below)
                {
                    current = i;
                }
            }

            return current;
        }
    }

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
