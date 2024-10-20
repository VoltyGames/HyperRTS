using HyperRTS.Core.Resources;
using UnityEngine;

namespace HyperRTS.Editor.Resource
{
    public class ResourceData : ScriptableObject
    {
        public string resourceId;
        public string resourceName;
        public Sprite resourceIcon;

        public static ResourceData Create(ResourceType resourceType, Sprite icon)
        {
            var resourceData = CreateInstance<ResourceData>();
            resourceData.resourceId = resourceType.Id.ToString();
            resourceData.resourceName = resourceType.DisplayName.ToString();
            resourceData.resourceIcon = icon;
            return resourceData;
        }
    }
}
