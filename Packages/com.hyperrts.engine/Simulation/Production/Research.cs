using Unity.Entities;

namespace HyperRTS.Simulation.Production
{
    /// <summary>
    /// Marks a producible that is researched rather than built: finishing it records a <see cref="CompletedResearch"/>
    /// on the owner instead of spawning anything. Each is researched once per player.
    /// </summary>
    public struct Research : IComponentData { }
}
