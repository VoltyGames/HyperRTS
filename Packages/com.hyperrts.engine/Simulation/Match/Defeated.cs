using Unity.Entities;

namespace HyperRTS.Simulation.Match
{
    /// <summary>Enabled on a player once it owns no <see cref="VictoryCritical"/> entity.</summary>
    public struct Defeated : IComponentData, IEnableableComponent { }
}
