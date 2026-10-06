using Unity.Entities;

namespace HyperRTS.Simulation.Production
{
    /// <summary>A buffer element naming a prefab its owner can build or train.</summary>
    public interface IPrefabOption
    {
        Entity Prefab { get; }
    }
}
