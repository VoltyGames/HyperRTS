using Unity.Entities;

namespace HyperRTS.Simulation.Combat
{
    /// <summary>Enabled by <see cref="DeathSystem"/> the frame an entity dies, just before it is destroyed.</summary>
    public struct Dead : IComponentData, IEnableableComponent { }
}
