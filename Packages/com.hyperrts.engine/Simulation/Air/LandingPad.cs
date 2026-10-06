using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Air
{
    /// <summary>One landing pad of an airfield and the aircraft that calls it home.</summary>
    [InternalBufferCapacity(4)]
    public struct LandingPad : IBufferElementData
    {
        public float3 Offset;

        /// <summary>The aircraft based here; <c>Entity.Null</c> while the pad is free.</summary>
        public Entity Aircraft;

        /// <summary>Index of the first free pad, or -1.</summary>
        public static int FindFree(DynamicBuffer<LandingPad> pads)
        {
            for (var i = 0; i < pads.Length; i++)
            {
                if (pads[i].Aircraft == Entity.Null)
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
