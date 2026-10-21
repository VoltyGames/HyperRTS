using HyperRTS.Simulation.Combat;
using Unity.Entities;

namespace HyperRTS.Simulation.Resources
{
    /// <summary>A resource node's <see cref="IModelStage"/> by its remaining share, from full to nearly spent.</summary>
    [InternalBufferCapacity(4)]
    public struct ResourceStage : IModelStage
    {
        public float Below;
        public Entity Visual;

        float IModelStage.Below => Below;

        Entity IModelStage.Visual => Visual;
    }

    /// <summary>The <see cref="ResourceStage"/> on screen; presentation keeps it, -1 until the first frame shows one.</summary>
    public struct ShownResourceStage : IComponentData
    {
        public int Index;
    }
}
