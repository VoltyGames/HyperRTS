using Unity.Entities;

namespace HyperRTS.Simulation.Resources
{
    /// <summary>A carrier on its way to <see cref="Source"/>; pays its owner on arrival and nothing if killed.</summary>
    public struct DeliveryCarrier : IComponentData
    {
        public Entity Source;
        public UnityObjectRef<ResourceType> Type;
        public int Amount;
    }
}
