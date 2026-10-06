using Unity.Entities;

namespace HyperRTS.Simulation.Production
{
    /// <summary>A unit prefab a producer can train.</summary>
    [InternalBufferCapacity(6)]
    public struct ProductionOption : IBufferElementData, IPrefabOption
    {
        public Entity Prefab;

        Entity IPrefabOption.Prefab => Prefab;
    }
}
