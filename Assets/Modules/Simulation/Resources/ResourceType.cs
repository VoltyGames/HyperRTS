using Unity.Collections;

namespace HyperRTS.Simulation.Resources
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
