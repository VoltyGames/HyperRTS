using Unity.Entities;

namespace HyperRTS.Simulation.Buildings
{
    /// <summary>A unit prefab a producer can train.</summary>
    [InternalBufferCapacity(6)]
    public struct ProductionOption : IBufferElementData, IPrefabOption
    {
        public Entity Prefab;

        Entity IPrefabOption.Prefab => Prefab;
    }
}
