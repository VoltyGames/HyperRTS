using Unity.Entities;
using UnityEngine;
using UnityEngine.Serialization;

namespace HyperRTS.Core.Resources
{
    public class ResourceAuthoring : MonoBehaviour
    {
        public ResourceType resourceType;
        public int amount;

        public class Baker : Baker<ResourceAuthoring>
        {
            public override void Bake(ResourceAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new Resource { Type = authoring.resourceType, Amount = authoring.amount });
            }
        }
    }
    
    public struct Resource : IComponentData
    {
        public ResourceType Type;
        public int Amount;
    }
}
