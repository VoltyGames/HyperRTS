using Unity.Entities;
using UnityEngine;
using UnityEngine.Serialization;

namespace HyperRTS.Core.Resources
{
    [AddComponentMenu(HyperRTSMenu.Resources + "Resource")]
    [Icon(HyperRTSIcons.Resources)]
    [HelpURL(HyperRTSDocs.WorldSetup)]
    [DisallowMultipleComponent]
    public class ResourceAuthoring : MonoBehaviour
    {
        [Tooltip("Type of resource this node yields.")]
        public ResourceType resourceType;

        [Tooltip("Amount of resource available.")]
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
