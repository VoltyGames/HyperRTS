using Unity.Entities;

namespace HyperRTS.Simulation.Match
{
    /// <summary>The owner stays in the match while it owns at least one of these.</summary>
    public struct VictoryCritical : IComponentData { }
}
