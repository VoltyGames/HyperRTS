using Unity.Entities;
using Unity.Netcode;

namespace HyperRTS.Simulation.Common.Infantry
{
    /// <summary>On while the squad runs for a retreat point, set by <c>RetreatSystem</c>; clients show it.</summary>
    [GhostEnabledBit]
    public struct Retreating : IComponentData, IEnableableComponent
    {
    }
}
