using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Presentation.Interaction
{
    /// <summary>
    /// Client-side building placement mode: the HUD starts it, input moves the ghost, presentation draws it.
    /// The confirmed placement still goes through a <c>PlaceBuilding</c> or <c>PlaceLine</c> command.
    /// </summary>
    public struct PlacementState : IComponentData
    {
        public bool Active;
        public Entity Prefab;

        /// <summary>Snapped ghost centre; for a line, where the leg being drawn ends.</summary>
        public float3 Position;

        /// <summary>A single building's snapped yaw in radians; lines yaw each segment along the line.</summary>
        public float Yaw;

        /// <summary>For a line, any segment is valid.</summary>
        public bool Valid;

        /// <summary>Confirm is held on a single building from <see cref="DragStart"/>, turning it.</summary>
        public bool Dragging;

        /// <summary>Where a single building's held Confirm went down, or where the wall leg drawn starts.</summary>
        public float3 DragStart;

        /// <summary>
        /// A wall is being drawn: its next leg runs from the last corner to <see cref="Position"/>. Finishing it
        /// leaves placement on, so another wall can start.
        /// </summary>
        public bool Drawing;
    }
}
