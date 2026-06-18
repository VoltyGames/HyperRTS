using System.Collections.Generic;
using Unity.Collections;

namespace HyperRTS.Core.Resources
{
    public static class ResourceManager
    {
        private static readonly Dictionary<FixedString64Bytes, ResourceType> ResourceTypes = new();
        
        /// <summary>
        /// Register a new resource type.
        /// If the resource type already exists, it will return the existing resource type.
        /// </summary>
        /// <param name="id">Resource unique ID</param>
        /// <param name="displayName">Resource display name</param>
        /// <returns>The registered resource type</returns>
        public static ResourceType CreateResource(FixedString64Bytes id, FixedString64Bytes? displayName = null)
        {
            if (ResourceTypes.ContainsKey(id))
                return ResourceTypes[id];

            var resourceType = new ResourceType
            {
                Id = id, 
                DisplayName = displayName ?? id
            };
            
            ResourceTypes.Add(id, resourceType);
            return resourceType;
        }

        /// <summary>
        /// Update or create a resource type. If the id already exists its display
        /// name is overwritten; otherwise a new entry is registered.
        /// </summary>
        /// <param name="id">Resource unique ID to find from the dictionary</param>
        /// <param name="displayName">Resource display name to update</param>
        /// <returns>The updated or created resource type</returns>
        public static ResourceType UpdateOrCreateResource(FixedString64Bytes id, FixedString64Bytes displayName)
        {
            var resourceType = new ResourceType { Id = id, DisplayName = displayName };
            ResourceTypes[id] = resourceType;
            return resourceType;
        }
        
        public static void RemoveResource(FixedString64Bytes id)
        {
            ResourceTypes.Remove(id);
        }
        
        public static ResourceType GetResourceType(FixedString64Bytes resourceId)
        {
            return ResourceTypes.GetValueOrDefault(resourceId);
        }
        
        public static IEnumerable<ResourceType> GetResourceTypes()
        {
            return ResourceTypes.Values;
        }
    }
}
