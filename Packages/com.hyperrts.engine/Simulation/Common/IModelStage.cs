using Unity.Collections;
using Unity.Entities;

namespace HyperRTS.Simulation.Common
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
}
