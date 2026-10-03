using Unity.Entities;

namespace HyperRTS.Simulation.Buildings
{
    /// <summary>A buffer element naming a prefab its owner can build or train.</summary>
    public interface IPrefabOption
    {
        Entity Prefab { get; }
    }
}
