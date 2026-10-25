using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Client.Interaction
{
    /// <summary>
    /// Client-side building placement mode: the HUD starts it, input moves the ghost, presentation draws it.
    /// The confirmed placement still goes through a <c>PlaceBuilding</c> or <c>PlaceLine</c> command.
    /// </summary>
    public struct PlacementState : IComponentData
    {
        public bool Active;
        public Entity Prefab;

        /// <summary>Snapped ghost centre; for a line, the drag's current end.</summary>
        public float3 Position;

        /// <summary>A single building's snapped yaw in radians; lines yaw each segment along the line.</summary>
        public float Yaw;

        /// <summary>For a line, any segment is valid.</summary>
        public bool Valid;

        /// <summary>Confirm is held from <see cref="DragStart"/>: a line drags its end, a single building turns.</summary>
        public bool Dragging;

        public float3 DragStart;
    }
}
