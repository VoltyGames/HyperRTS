using System.Collections.Generic;
using Unity.Collections;

namespace HyperRTS.Core.Resources
{
    public static class ResourceRegistry
    {
        private static int _nextId = 0;
        private static readonly Dictionary<FixedString64Bytes, ResourceType> ResourceTypes = new();
        
        public static ResourceType RegisterResourceType(FixedString64Bytes name)
        {
            if (ResourceTypes.ContainsKey(name))
                return ResourceTypes[name];

            var resourceType = new ResourceType { Id = _nextId++, Name = name };
            ResourceTypes.Add(name, resourceType);
            return resourceType;
        }
        
        public static ResourceType GetResourceType(FixedString64Bytes name)
        {
            return ResourceTypes.GetValueOrDefault(name);
        }
        
        public static IEnumerable<ResourceType> GetResourceTypes()
        {
            return ResourceTypes.Values;
        }
    }
}
