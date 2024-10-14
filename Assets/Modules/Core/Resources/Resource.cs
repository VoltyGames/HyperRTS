using Unity.Entities;

namespace HyperRTS.Core.Resources
{
    public struct Resource : IComponentData
    {
        public ResourceType Type;
        public int Amount;
    }
}
