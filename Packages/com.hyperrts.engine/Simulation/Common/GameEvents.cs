using Unity.Entities;

namespace HyperRTS.Simulation.Common
{
    /// <summary>
    /// Tags the singleton holding this frame's gameplay events (<see cref="UnitProduced"/>,
    /// <see cref="BuildingCompleted"/>, <see cref="OwnerChanged"/>). <c>GameEventSystem</c> clears them at the start of
    /// the order phase; sound, stats, cleanup and game systems react to them later in the frame.
    /// </summary>
    public struct GameEvents : IComponentData { }
}
