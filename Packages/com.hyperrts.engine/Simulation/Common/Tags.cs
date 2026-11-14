using Unity.Entities;

namespace HyperRTS.Simulation.Common
{
    /// <summary>Marks a movable, orderable unit.</summary>
    public struct UnitTag : IComponentData { }

    /// <summary>Marks a static structure.</summary>
    public struct BuildingTag : IComponentData { }

    /// <summary>
    /// This frame's deaths: on from <c>DeathSystem</c> until the end-of-frame buffer destroys them, never replicated.
    /// Read it only in the lifecycle phase after <c>DeathSystem</c>; elsewhere, alive means <c>Health.IsAlive</c>.
    /// </summary>
    public struct Dead : IComponentData, IEnableableComponent { }

    /// <summary>
    /// Scenery or remains removed without remains, effects or sounds, such as by a building site.
    /// <c>DeathSystem</c> destroys it the next lifecycle phase.
    /// </summary>
    public struct SceneryCleared : IComponentData { }
}
