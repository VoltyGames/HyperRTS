using Unity.Entities;
using Unity.NetCode;

namespace HyperRTS.Simulation.Vision
{
    /// <summary>Sight radius revealing fog of war for the owner's team.</summary>
    public struct VisionRange : IComponentData
    {
        [GhostField] public float Value;
    }

    /// <summary>Hostile root the local player can't see. Selection, picking, HUD and rendering all skip it.</summary>
    public struct FogHidden : IComponentData { }
}
