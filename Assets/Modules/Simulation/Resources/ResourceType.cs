using System;
using Unity.Collections;

namespace HyperRTS.Simulation.Resources
{
    [Serializable]
    public struct ResourceType
    {
        public FixedString64Bytes Id;
        public FixedString64Bytes DisplayName;
    }
}
