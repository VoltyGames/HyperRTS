using Unity.Collections;
using Unity.Entities;

namespace HyperRTS.Simulation.Resources
{
    /// <summary>
    /// One look of a resource node: its <see cref="Visual"/> child shows while the node's remaining share is at or
    /// below <see cref="Below"/> and above the next stage's. Stages are ordered from full to nearly spent.
    /// </summary>
    [InternalBufferCapacity(4)]
    public struct ResourceStage : IBufferElementData
    {
        public float Below;
        public Entity Visual;

        /// <summary>The emptiest stage whose threshold the remaining share is at or below.</summary>
        public static int Pick(NativeArray<ResourceStage> stages, float remaining)
        {
            var current = 0;
            for (var i = 0; i < stages.Length; i++)
            {
                if (remaining <= stages[i].Below)
                {
                    current = i;
                }
            }

            return current;
        }
    }

    /// <summary>The <see cref="ResourceStage"/> on screen; presentation keeps it, -1 until the first frame shows one.</summary>
    public struct ShownResourceStage : IComponentData
    {
        public int Index;
    }
}
