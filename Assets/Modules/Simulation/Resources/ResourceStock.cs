using Unity.Entities;

namespace HyperRTS.Simulation.Resources
{
    /// <summary>A player's stockpile, one element per resource type held.</summary>
    [InternalBufferCapacity(4)]
    public struct ResourceStock : IBufferElementData
    {
        public UnityObjectRef<ResourceType> Type;
        public int Amount;
    }
}
