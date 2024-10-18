using HyperRTS.Core.Resources;
using UnityEngine;

namespace HyperRTS.Editor.Resource
{
    [CreateAssetMenu(fileName = "NewResource", menuName = "RTS Engine/Resource Data", order = 0)]
    public class ResourceData : ScriptableObject
    {
        public ResourceType resourceType;
        public Sprite resourceIcon;
        
        /// <summary>
        /// Copy data from another ResourceData instance (used when saving as a prefab)
        /// </summary>
        public void CopyFrom(ResourceData other)
        {
            resourceType = other.resourceType;
            resourceIcon = other.resourceIcon;
        }
    }
}
