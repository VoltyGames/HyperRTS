using Unity.Entities;

namespace HyperRTS.Presentation.Physics
{
    /// <summary>The building or vehicle a box collider in the presented physics world stands for.</summary>
    public struct ColliderSource : IComponentData
    {
        public Entity Value;
    }
}
