using Unity.Entities;
using Unity.NetCode;

namespace HyperRTS.Simulation.Common
{
    /// <summary>Enabled on a player once it owns no <c>VictoryCritical</c> entity.</summary>
    [GhostEnabledBit]
    public struct Defeated : IComponentData, IEnableableComponent { }
}
