using Unity.Entities;

namespace HyperRTS.Simulation.Upgrades
{
    /// <summary>Marks an upgrade prefab: producing it researches it instead of spawning anything.</summary>
    public struct Upgrade : IComponentData { }
}
