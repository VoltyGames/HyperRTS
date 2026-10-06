using Unity.Entities;
using Unity.NetCode;

namespace HyperRTS.Simulation.Vision
{
    /// <summary>Enabled while stealth hides the entity right now: invisible and untargetable to undetecting enemies.</summary>
    [GhostEnabledBit]
    public struct Stealthed : IComponentData, IEnableableComponent { }
}
