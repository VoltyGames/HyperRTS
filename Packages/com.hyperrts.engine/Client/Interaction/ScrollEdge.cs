using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Client.Interaction
{
    /// <summary>Written by the camera, read by the cursor: the screen edge the pointer is scrolling against.</summary>
    public struct ScrollEdge : IComponentData
    {
        /// <summary>-1, 0 or 1 per axis; +y is the top of the screen.</summary>
        public int2 Direction;

        /// <summary>Set by input that steers with the pointer, such as turning a building, so the camera holds still.</summary>
        public bool Paused;

        /// <summary>The edge a pointer touches within <paramref name="border"/> pixels, zero off screen or inside.</summary>
        public static int2 At(float2 pointer, float2 screen, float border)
        {
            var offScreen = math.any(pointer < 0f) || math.any(pointer > screen);
            if (offScreen)
            {
                return int2.zero;
            }

            var low = math.select(int2.zero, 1, pointer <= border);
            var high = math.select(int2.zero, 1, pointer >= screen - border);
            return high - low;
        }
    }
}
