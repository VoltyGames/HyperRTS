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

        /// <summary>Snapped ghost centre under the cursor; for a line, the drag's current end.</summary>
        public float3 Position;

        /// <summary>For a line, any segment is valid.</summary>
        public bool Valid;

        /// <summary>A line prefab's drag is held down from <see cref="LineStart"/>.</summary>
        public bool Dragging;

        public float3 LineStart;

        /// <summary>Valid <see cref="PlacementSegment"/>s, so the HUD can show the line's total cost.</summary>
        public int ValidSegments;
    }
}
