using Unity.Entities;
using Unity.NetCode;

namespace HyperRTS.Simulation.Match
{
    /// <summary>Enabled on a player once it owns no <see cref="VictoryCritical"/> entity.</summary>
    [GhostEnabledBit]
    public struct Defeated : IComponentData, IEnableableComponent { }
}
