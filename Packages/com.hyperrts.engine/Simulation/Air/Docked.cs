using Unity.Entities;
using Unity.NetCode;

namespace HyperRTS.Simulation.Air
{
    /// <summary>
    /// Enabled while the aircraft sits landed on its pad: ammo reloads, and games refuel or repair it. Any move lifts
    /// it off again.
    /// </summary>
    [GhostEnabledBit]
    public struct Docked : IComponentData, IEnableableComponent
    {
        public static bool Of(in ComponentLookup<Docked> lookup, Entity entity) =>
            lookup.HasComponent(entity) && lookup.IsComponentEnabled(entity);
    }
}
