using Unity.Entities;

namespace HyperRTS.Simulation.Resources
{
    /// <summary>Gathering stats and the cargo currently carried.</summary>
    public struct Harvester : IComponentData
    {
        public int Capacity;
        public float GatherRate;

        public UnityObjectRef<ResourceType> CargoType;
        public int CargoAmount;
    }
}
