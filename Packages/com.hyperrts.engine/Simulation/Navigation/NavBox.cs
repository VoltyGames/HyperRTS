using Unity.Mathematics;

namespace HyperRTS.Simulation.Navigation
{
    /// <summary>An XZ box turned about Y, as obstacles, areas and buildings stamp themselves onto the nav grid.</summary>
    public readonly struct NavBox
    {
        private const float AxisTolerance = 1e-3f;

        public readonly float2 Center;
        public readonly float2 Right;
        public readonly float2 Forward;
        public readonly float2 Half;

        public NavBox(float3 position, quaternion rotation, float2 size)
        {
            Center = position.xz;
            Right = math.normalizesafe(math.mul(rotation, math.right()).xz, new float2(1f, 0f));
            Forward = math.normalizesafe(math.mul(rotation, math.forward()).xz, new float2(0f, 1f));
            Half = size * 0.5f;
        }

        /// <summary>Half size of the world-axis box around it.</summary>
        public float2 Extents => new(
            math.abs(Right.x) * Half.x + math.abs(Forward.x) * Half.y,
            math.abs(Right.y) * Half.x + math.abs(Forward.y) * Half.y);

        /// <summary>Turned by a multiple of 90°, so its world-axis box is the box itself.</summary>
        public bool IsAxisAligned => math.abs(Right.x * Right.y) < AxisTolerance;

        /// <summary>Whether a point lies inside the box grown by <paramref name="margin"/> on every side.</summary>
        public bool Contains(float2 point, float margin)
        {
            var offset = point - Center;
            return math.abs(math.dot(offset, Right)) <= Half.x + margin
                   && math.abs(math.dot(offset, Forward)) <= Half.y + margin;
        }
    }
}
