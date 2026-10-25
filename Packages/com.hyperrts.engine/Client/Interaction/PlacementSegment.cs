using HyperRTS.Simulation.Buildings;
using Unity.Entities;

namespace HyperRTS.Client.Interaction
{
    /// <summary>One ghost segment of a line placement, on the <see cref="PlacementState"/> singleton.</summary>
    [InternalBufferCapacity(0)]
    public struct PlacementSegment : IBufferElementData
    {
        /// <summary>Grounded and yawed along the line, as the server will check it.</summary>
        public BuildingSpot Spot;

        /// <summary>The cells under it allow it, cached until the grid changes.</summary>
        public bool Ground;

        /// <summary><see cref="Ground"/> with no seen enemy unit on it.</summary>
        public bool Valid;

        public static int CountValid(DynamicBuffer<PlacementSegment> segments)
        {
            var count = 0;
            foreach (var segment in segments)
            {
                count += segment.Valid ? 1 : 0;
            }

            return count;
        }
    }
}
