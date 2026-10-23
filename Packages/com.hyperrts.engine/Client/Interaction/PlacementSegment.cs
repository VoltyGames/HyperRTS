using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Client.Interaction
{
    /// <summary>One ghost segment of a line placement, on the <see cref="PlacementState"/> singleton.</summary>
    [InternalBufferCapacity(0)]
    public struct PlacementSegment : IBufferElementData
    {
        public float3 Center;

        /// <summary>The segment's footprint as turned along the line.</summary>
        public float2 Footprint;

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
