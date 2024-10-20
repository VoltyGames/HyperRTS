using Unity.Collections;

namespace HyperRTS.Core.Resources
{
    /// <summary>
    /// Defines a resource type.
    /// </summary>
    public struct ResourceType
    {
        public FixedString64Bytes Id;
        public FixedString64Bytes DisplayName;
    }
}
