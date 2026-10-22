using Unity.Entities;
using Unity.Netcode;

namespace HyperRTS.Simulation.Vision
{
    /// <summary>Sight radius revealing fog of war for the owner's team.</summary>
    public struct VisionRange : IComponentData
    {
        [GhostField] public float Value;

        /// <summary>Eye height above the viewer's ground or its own position, whichever is higher.</summary>
        public float EyeHeight;
    }

    /// <summary>Hostile root the local player can't see. Selection, picking, HUD and rendering all skip it.</summary>
    public struct FogHidden : IComponentData { }
}
