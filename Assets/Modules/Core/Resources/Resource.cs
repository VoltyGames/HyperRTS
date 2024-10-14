using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Core.Resources
{
    public struct Resource : IComponentData
    {
        public ResourceType Type;
        public int Amount;
    }

    public class ResourceAuthoring : MonoBehaviour
    {
        public ResourceType Type;
        public int Amount;

        public class Baker : Baker<ResourceAuthoring>
        {
            public override void Bake(ResourceAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new Resource { Type = authoring.Type, Amount = authoring.Amount });
            }
        }
    }
}
