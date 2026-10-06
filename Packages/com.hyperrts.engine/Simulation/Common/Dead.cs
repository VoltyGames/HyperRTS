using Unity.Entities;

namespace HyperRTS.Simulation.Common
{
    /// <summary>Enabled by <c>DeathSystem</c> the frame an entity dies, just before it is destroyed.</summary>
    public struct Dead : IComponentData, IEnableableComponent { }
}
