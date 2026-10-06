using Unity.Entities;

namespace HyperRTS.Simulation.Power
{
    /// <summary>
    /// Enabled on a power consumer while its owner's power is low: its weapons hold fire and production slows.
    /// Games read it for anything else that needs power (radar, special buildings).
    /// </summary>
    public struct Unpowered : IComponentData, IEnableableComponent { }
}
