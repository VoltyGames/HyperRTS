using HyperRTS.Simulation.Stats;
using Unity.Entities;
using Unity.Netcode;

namespace HyperRTS.Simulation.Vision
{
    /// <summary>Sight radius revealing fog of war for the owner's team.</summary>
    public struct VisionRange : IComponentData
    {
        [GhostField] [StatField(Stat.VisionRange)] public float Value;

        /// <summary>Eye height above the viewer's ground or its own position, whichever is higher.</summary>
        public float EyeHeight;
    }

    /// <summary>
    /// A target as tall as a vehicle or a building: seen wherever <see cref="FogOfWar.VisibleTall"/> is, so a low wall
    /// hides infantry behind it but not a tank.
    /// </summary>
    public struct TallTarget : IComponentData { }

    /// <summary>Hostile root the local player can't see. Selection, picking, HUD and rendering all skip it.</summary>
    public struct FogHidden : IComponentData { }
}
