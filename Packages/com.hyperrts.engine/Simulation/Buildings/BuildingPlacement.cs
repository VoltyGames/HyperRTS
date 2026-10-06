using HyperRTS.Simulation.Interaction;
using Unity.Entities;

namespace HyperRTS.Simulation.Buildings
{
    /// <summary>Where a building may be placed; buildings without it go on land.</summary>
    public struct BuildingPlacement : IComponentData
    {
        public PlacementSurface Surface;
    }
}
