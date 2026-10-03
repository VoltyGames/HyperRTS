using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Interaction
{
    /// <summary>
    /// Client-side building placement mode: the HUD starts it, input moves the ghost, presentation draws it.
    /// The confirmed placement still goes through a <c>PlaceBuilding</c> command.
    /// </summary>
    public struct PlacementState : IComponentData
    {
        public bool Active;
        public Entity Prefab;
        public float2 Footprint;

        /// <summary>Snapped ghost centre under the cursor.</summary>
        public float3 Position;

        public bool Valid;
    }
}
