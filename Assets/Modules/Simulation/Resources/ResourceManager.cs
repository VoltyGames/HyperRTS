using System.Collections.Generic;
using Unity.Collections;

namespace HyperRTS.Simulation.Resources
{
    public static class ResourceManager
    {
        private static readonly Dictionary<FixedString64Bytes, ResourceType> ResourceTypes = new();
        
        /// <summary>Registers a resource type, or returns the existing one.</summary>
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

        /// <summary>Registers or overwrites a resource type.</summary>
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
