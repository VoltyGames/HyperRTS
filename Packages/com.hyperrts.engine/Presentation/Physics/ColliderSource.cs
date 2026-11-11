using Unity.Entities;
using Unity.Transforms;

namespace HyperRTS.Presentation.Physics
{
    /// <summary>The building or vehicle a box collider in the presented physics world stands for.</summary>
    public struct ColliderSource : IComponentData
    {
        public Entity Value;

        /// <summary>Where the source stood this step, read by the system before it drives the box.</summary>
        public LocalTransform Target;

        /// <summary>False when the source is gone and <see cref="Target"/> is stale.</summary>
        public bool Present;
    }
}
