using Unity.Mathematics;

namespace HyperRTS.Simulation.Selection
{
    /// <summary>Pure selection math. Screen space is pixels with a bottom-left origin.</summary>
    public static class SelectionMath
    {
        /// <summary>Rect from two corners in any order, edges inclusive.</summary>
        public static bool RectContains(float2 a, float2 b, float2 point)
        {
            var min = math.min(a, b);
            var max = math.max(a, b);
            return point.x >= min.x && point.x <= max.x &&
                   point.y >= min.y && point.y <= max.y;
        }

        /// <summary>World point to screen pixels; false when behind the camera.</summary>
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

        /// <summary>No modifier replaces, Shift adds, Ctrl removes; Ctrl wins.</summary>
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

        /// <summary>Drag-box priority: owned units over owned buildings over everything else.</summary>
        public static int DragRank(bool owned, bool isUnit, bool isBuilding)
        {
            if (!owned)
            {
                return 0;
            }

            return isUnit ? 2 : isBuilding ? 1 : 0;
        }
    }
}
