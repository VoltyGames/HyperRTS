using Unity.Entities;

namespace HyperRTS.Simulation.Common
{
    /// <summary>Marks a movable, orderable unit.</summary>
    public struct UnitTag : IComponentData { }

    /// <summary>Marks a static structure.</summary>
    public struct BuildingTag : IComponentData { }

    /// <summary>Enabled by <c>DeathSystem</c> the frame an entity dies, just before it is destroyed.</summary>
    public struct Dead : IComponentData, IEnableableComponent { }

    /// <summary>
    /// Scenery or remains removed without remains, effects or sounds, such as by a building site.
    /// <c>DeathSystem</c> destroys it the next lifecycle phase.
    /// </summary>
    public struct Cleared : IComponentData { }
}
