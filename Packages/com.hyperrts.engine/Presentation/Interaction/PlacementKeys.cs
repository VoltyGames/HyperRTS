using Unity.Collections;
using Unity.Entities;

namespace HyperRTS.Presentation.Interaction
{
    /// <summary>
    /// Client singleton: the display names of the keys that place, turn, snap and finish a placement, which input
    /// writes and the hint shows.
    /// </summary>
    public struct PlacementKeys : IComponentData
    {
        public FixedString32Bytes Place;
        public FixedString32Bytes Rotate;
        public FixedString32Bytes Snap;

        /// <summary>Finishes a wall being drawn, or leaves placement.</summary>
        public FixedString32Bytes Finish;

        public readonly bool Equals(in PlacementKeys other) =>
            Place == other.Place && Rotate == other.Rotate && Snap == other.Snap && Finish == other.Finish;
    }
}
