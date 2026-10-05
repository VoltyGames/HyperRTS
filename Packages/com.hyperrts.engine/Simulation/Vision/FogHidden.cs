using Unity.Entities;

namespace HyperRTS.Simulation.Vision
{
    /// <summary>Hostile root the local player can't see. Selection, picking, HUD and rendering all skip it.</summary>
    public struct FogHidden : IComponentData { }
}
