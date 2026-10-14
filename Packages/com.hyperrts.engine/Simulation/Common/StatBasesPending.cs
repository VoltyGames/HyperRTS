using Unity.Entities;

namespace HyperRTS.Simulation.Common
{
    /// <summary>Enabled on a fresh entity until its <see cref="BaseStat"/>s are recorded from the authored values.</summary>
    public struct StatBasesPending : IComponentData, IEnableableComponent
    {
    }
}
