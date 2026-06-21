using Unity.Mathematics;

namespace HyperRTS.Simulation.Selection
{
    /// <summary>
    /// Burst-friendly, side-effect-free selection helpers, testable without a camera or ECS world.
    /// Screen space is pixels, bottom-left origin (matches Mouse.position / Camera.WorldToScreenPoint).
    /// </summary>
    public static class SelectionMath
    {
        /// <summary>Point in the rectangle spanned by <paramref name="a"/>/<paramref name="b"/> (any corner order, edges inclusive).</summary>
        public static bool RectContains(float2 a, float2 b, float2 point)
        {
            var min = math.min(a, b);
            var max = math.max(a, b);
            return point.x >= min.x && point.x <= max.x &&
                   point.y >= min.y && point.y <= max.y;
        }

        /// <summary>Projects a world point to screen pixels via <c>projection * worldToCamera</c>; false when behind the camera.</summary>
        public static bool WorldToScreenPoint(float4x4 viewProjection, float3 world, float2 screenSize,
            out float2 screen)
        {
            var clip = math.mul(viewProjection, new float4(world, 1f));

            if (clip.w <= 0f)
            {
                screen = default;
                return false;
            }

            var ndc = clip.xy / clip.w;
            screen = (ndc * 0.5f + 0.5f) * screenSize;
            return true;
        }

        /// <summary>No modifier replaces (selected iff hit), Shift adds, Ctrl removes; subtract wins if both held.</summary>
        public static bool ResolveSelected(bool current, bool hit, bool additive, bool subtract)
        {
            if (subtract)
            {
                return current && !hit;
            }

            if (additive)
            {
                return current || hit;
            }

            return hit;
        }
    }
}
