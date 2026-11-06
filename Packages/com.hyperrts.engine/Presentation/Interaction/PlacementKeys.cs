using Unity.Collections;
using Unity.Entities;

namespace HyperRTS.Presentation.Interaction
{
    /// <summary>Client singleton: the display names of the keys that turn and snap a placement, which input writes and the hint shows.</summary>
    public struct PlacementKeys : IComponentData
    {
        public FixedString32Bytes Rotate;
        public FixedString32Bytes Snap;
    }
}
